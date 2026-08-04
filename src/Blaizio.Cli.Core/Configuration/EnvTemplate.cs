using System.Text;

namespace Blaizio.Cli.Core.Configuration;

/// <summary>
/// Expands <c>${VAR}</c> references from the process environment. The one place a secret enters
/// the CLI: config files hold the variable's name, the value arrives here, and nothing writes it
/// back out.
/// </summary>
public static class EnvTemplate
{
    /// <summary>
    /// Replace every <c>${VAR}</c> in <paramref name="value"/> with its environment variable.
    /// Unset variables are reported through <paramref name="missing"/> and left as they were
    /// written, so a caller can name them instead of sending a half-filled credential.
    /// </summary>
    public static string Expand(string value, out IReadOnlyList<string> missing)
    {
        var absent = new List<string>();
        missing = absent;

        var open = value.IndexOf("${", StringComparison.Ordinal);
        if (open < 0)
            return value;

        var result = new StringBuilder(value.Length);
        var cursor = 0;
        while (open >= 0)
        {
            var close = value.IndexOf('}', open + 2);
            if (close < 0)
                break;

            var name = value[(open + 2)..close];
            result.Append(value, cursor, open - cursor);

            var resolved = name.Length == 0 ? null : Environment.GetEnvironmentVariable(name);
            if (resolved is null)
            {
                // Left verbatim: the caller reports the name rather than sending "Bearer " and
                // letting the registry answer 401 for what is a local misconfiguration.
                if (name.Length > 0)
                    absent.Add(name);
                result.Append(value, open, close - open + 1);
            }
            else
            {
                result.Append(resolved);
            }

            cursor = close + 1;
            open = value.IndexOf("${", cursor, StringComparison.Ordinal);
        }

        result.Append(value, cursor, value.Length - cursor);
        return result.ToString();
    }

    /// <summary>True when the value references at least one environment variable.</summary>
    public static bool ReferencesEnv(string value) =>
        value.Contains("${", StringComparison.Ordinal) && value.Contains('}', StringComparison.Ordinal);
}
