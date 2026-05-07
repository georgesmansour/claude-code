using InvitationPlatform.Domain.Enums;

namespace InvitationPlatform.Domain.Entities;

public class InvitationSection
{
    public Guid Id { get; set; }
    public Guid InvitationId { get; set; }
    public SectionType Type { get; set; }
    public bool Enabled { get; set; } = true;
    public int OrderIndex { get; set; } = 0;

    /// <summary>
    /// Type-specific scalar fields stored as JSONB.
    /// cover:     { eventLabel, names, tagline, hostText, hostIntro, hostOutro, image, buttonText }
    /// countdown: { label, date, description, image }
    /// locations: { label, title, image }
    /// gifts:     { label, title, description, image }
    /// rsvp:      { label, title, deadline, image }
    /// custom:    { label, title, body, image }
    /// </summary>
    public string Config { get; set; } = "{}";

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation
    public Invitation Invitation { get; set; } = null!;
    public ICollection<Location> Locations { get; set; } = [];
    public ICollection<GiftAccount> GiftAccounts { get; set; } = [];
}
