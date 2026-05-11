namespace InvitationPlatform.Domain.Entities;

public class Location
{
    public Guid Id { get; set; }
    public Guid SectionId { get; set; }
    public int OrderIndex { get; set; } = 0;

    /// <summary>e.g. "6:30 PM — Ceremony"</summary>
    public string? TimeLabel { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? MapUrl { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }

    // Navigation
    public InvitationSection Section { get; set; } = null!;
}
