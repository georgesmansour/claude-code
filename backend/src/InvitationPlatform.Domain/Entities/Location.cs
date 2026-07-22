namespace InvitationPlatform.Domain.Entities;

public class Location
{
    public Guid Id { get; set; }
    public Guid SectionId { get; set; }
    public int OrderIndex { get; set; } = 0;

    /// <summary>e.g. "6:30 PM — Ceremony"</summary>
    public string? TimeLabel { get; set; }
    /// <summary>Card heading, e.g. "Wedding Ceremony"</summary>
    public string? Label { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? MapUrl { get; set; }
    /// <summary>Optional venue photo shown on the location card.</summary>
    public string? ImageUrl { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }

    // Navigation
    public InvitationSection Section { get; set; } = null!;
}
