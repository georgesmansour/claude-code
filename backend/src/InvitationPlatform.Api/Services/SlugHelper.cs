using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace InvitationPlatform.Api.Services;

/// <summary>
/// Turns a guest's name into a URL-friendly slug (e.g. "Charbel Nahhas" → "charbel-nahhas").
/// Uniqueness is enforced by the callers, which append a numeric or random suffix when a
/// candidate is already taken.
/// </summary>
public static partial class SlugHelper
{
    /// <summary>Base slug from a display name — lowercase, ASCII, hyphen-separated.</summary>
    public static string Slugify(string? name)
    {
        var s = RemoveDiacritics((name ?? string.Empty).Trim().ToLowerInvariant());
        s = NonSlugChars().Replace(s, "-").Trim('-');
        return s.Length == 0 ? "guest" : Truncate(s, 120);
    }

    /// <summary>Short lowercase alphanumeric suffix used to force a fresh, unguessable link.</summary>
    public static string RandomSuffix(int length = 4)
    {
        const string alphabet = "abcdefghijklmnopqrstuvwxyz0123456789";
        var bytes = System.Security.Cryptography.RandomNumberGenerator.GetBytes(length);
        var sb = new StringBuilder(length);
        foreach (var b in bytes) sb.Append(alphabet[b % alphabet.Length]);
        return sb.ToString();
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max].TrimEnd('-');

    private static string RemoveDiacritics(string text)
    {
        var normalized = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonSlugChars();
}
