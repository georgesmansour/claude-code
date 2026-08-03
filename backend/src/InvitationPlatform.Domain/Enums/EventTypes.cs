namespace InvitationPlatform.Domain.Enums;

/// <summary>
/// Known event categories for templates. Deliberately plain strings (not a C# enum) so a new
/// category can be introduced without a schema change or migration — add it here and it appears
/// in the admin filter automatically.
/// </summary>
public static class EventTypes
{
    public const string Wedding    = "Wedding";
    public const string Birthday   = "Birthday";
    public const string Engagement = "Engagement";
    public const string BabyShower = "Baby Shower";
    public const string Graduation = "Graduation";
    public const string Corporate  = "Corporate";

    /// <summary>All categories offered in the UI, in display order.</summary>
    public static readonly IReadOnlyList<string> All =
        [Wedding, Engagement, Birthday, BabyShower, Graduation, Corporate];

    /// <summary>URL-safe key used in template folder paths, e.g. "Baby Shower" → "baby-shower".</summary>
    public static string ToKey(string? eventType) =>
        string.IsNullOrWhiteSpace(eventType)
            ? "wedding"
            : eventType.Trim().ToLowerInvariant().Replace(' ', '-');
}
