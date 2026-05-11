namespace InvitationPlatform.Domain.Entities;

public class RsvpGuest
{
    public Guid Id { get; set; }
    public Guid RsvpId { get; set; }
    public int OrderIndex { get; set; } = 0;

    public string FullName { get; set; } = string.Empty;

    /// <summary>adult | child | infant</summary>
    public string? AgeGroup { get; set; }
    public string? MealPreference { get; set; }
    public string? DietaryRestrictions { get; set; }

    // Navigation
    public Rsvp Rsvp { get; set; } = null!;
}
