namespace Blaizio;

/// <summary>
/// Ready-made regular-expression sources for <see cref="BaseInputOtp.Pattern"/>. The pattern is tested
/// against the WHOLE value on every edit, so a partially-typed value still has to match - use anchored,
/// one-or-more patterns like these.
/// </summary>
public static class InputOtpPatterns
{
    /// <summary>Digits only (<c>0-9</c>).</summary>
    public const string Digits = "^[0-9]+$";

    /// <summary>Letters only (<c>a-z</c>, <c>A-Z</c>).</summary>
    public const string Chars = "^[a-zA-Z]+$";

    /// <summary>Letters and digits.</summary>
    public const string DigitsAndChars = "^[a-zA-Z0-9]+$";
}
