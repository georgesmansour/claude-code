using System.Text.RegularExpressions;

namespace InvitationPlatform.Api.Services;

/// <summary>
/// Normalisation and validation for client contact details (email / phone). Centralised so the
/// create, update and login paths all treat identifiers identically — the single source of truth
/// for "what counts as the same email/phone".
/// </summary>
public static partial class ContactHelper
{
    /// <summary>Trimmed, lower-cased email, or null when empty.</summary>
    public static string? NormalizeEmail(string? email)
    {
        var e = email?.Trim().ToLowerInvariant();
        return string.IsNullOrEmpty(e) ? null : e;
    }

    /// <summary>
    /// Canonical phone form for storage and comparison: keeps a single leading "+" and digits only
    /// (spaces, dashes and brackets are stripped). Returns null when there are no digits.
    /// </summary>
    public static string? NormalizePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return null;
        var hasPlus = phone.TrimStart().StartsWith('+');
        var digits = NonDigits().Replace(phone, "");
        if (digits.Length == 0) return null;
        return hasPlus ? "+" + digits : digits;
    }

    /// <summary>True when the string looks like an email (used to route login identifiers).</summary>
    public static bool LooksLikeEmail(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Contains('@');

    /// <summary>Basic email shape check (kept lenient — real validation is delivery, not regex).</summary>
    public static bool IsValidEmail(string email) => EmailShape().IsMatch(email);

    [GeneratedRegex(@"\D")]
    private static partial Regex NonDigits();

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailShape();
}
