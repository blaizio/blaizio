using System.Linq.Expressions;
using System.Reflection;

namespace Blaizio.Ui;

/// <summary>
/// Reads a <see cref="BzField.For"/> expression such as <c>() =&gt; model.Address.City</c>: the
/// object the member belongs to (<c>model.Address</c>), the member's name, and its current value.
/// </summary>
/// <remarks>
/// <para>
/// The expression is a new object on every render (a lambda in markup allocates its tree each
/// time), so nothing here is compiled or cached per expression: the path is walked and each step
/// read through the member the expression itself names. That costs a few reflection reads per
/// render and stays correct when the objects along the path are replaced.
/// </para>
/// <para>
/// Safe in a trimmed app: every member comes out of the expression tree, where the compiler put a
/// direct reference to it, so the trimmer keeps it. No member is looked up by name.
/// </para>
/// </remarks>
internal static class FieldExpression
{
    /// <summary>
    /// Resolves <paramref name="accessor"/>. Returns <see langword="false"/> when it does not end
    /// in a field or property of an object (a method call, an indexer, a static member), or when
    /// an object along the path is <see langword="null"/>.
    /// </summary>
    public static bool TryRead(Expression<Func<object?>> accessor, out object model, out string name, out object? value)
    {
        model = default!;
        name = string.Empty;
        value = null;

        if (Unwrap(accessor.Body) is not MemberExpression { Expression: { } owner } member) return false;
        if (!TryEvaluate(owner, out var target) || target is null) return false;

        model = target;
        name = member.Member.Name;
        return TryGet(member.Member, target, out value);
    }

    // A value-typed member is boxed for Func<object?>: the compiler wraps it in a Convert node.
    private static Expression Unwrap(Expression expression) =>
        expression is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } convert
            ? Unwrap(convert.Operand)
            : expression;

    private static bool TryEvaluate(Expression? expression, out object? value)
    {
        switch (expression is null ? null : Unwrap(expression))
        {
            // The component ("this") or the object holding the captured locals.
            case ConstantExpression constant:
                value = constant.Value;
                return true;

            case MemberExpression member:
                if (member.Expression is null)
                    return TryGet(member.Member, null, out value); // a static member starts the path
                if (TryEvaluate(member.Expression, out var owner) && owner is not null)
                    return TryGet(member.Member, owner, out value);
                value = null;
                return false;

            // items[i], lookup["key"]: an indexer is a call to its getter.
            case MethodCallExpression { Method.IsSpecialName: true } call when call.Method.Name.StartsWith("get_", StringComparison.Ordinal):
                value = null;
                object? instance = null;
                if (call.Object is not null && (!TryEvaluate(call.Object, out instance) || instance is null)) return false;
                var arguments = new object?[call.Arguments.Count];
                for (var i = 0; i < arguments.Length; i++)
                {
                    if (!TryEvaluate(call.Arguments[i], out arguments[i])) return false;
                }
                value = call.Method.Invoke(instance, arguments);
                return true;

            // array[i]
            case BinaryExpression { NodeType: ExpressionType.ArrayIndex } index:
                value = null;
                if (!TryEvaluate(index.Left, out var array) || array is not Array items) return false;
                if (!TryEvaluate(index.Right, out var position) || position is not int at) return false;
                value = items.GetValue(at);
                return true;

            default:
                value = null;
                return false;
        }
    }

    private static bool TryGet(MemberInfo member, object? owner, out object? value)
    {
        switch (member)
        {
            case PropertyInfo property:
                value = property.GetValue(owner);
                return true;
            case FieldInfo field:
                value = field.GetValue(owner);
                return true;
            default:
                value = null;
                return false;
        }
    }
}
