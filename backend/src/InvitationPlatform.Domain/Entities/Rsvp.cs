using InvitationPlatform.Domain.Enums;

namespace InvitationPlatform.Domain.Entities;

public class Rsvp
{
    public Guid Id { get; set; }
    public Guid InvitationId { get; set; }

    /// <summary>
    /// When the response came through a personal guest link, this ties the RSVP to that
    /// guest so re-submissions update the same record instead of creating duplicates.
    /// Null for anonymous submissions via the generic link.
    /// </summary>
    public Guid? GuestId { get; set; }

    public RsvpResponse Response { get; set; }
    public int PartySize { get; set; } = 0;

    public string? ContactName { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string? Message { get; set; }

    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigation
    public Invitation Invitation { get; set; } = null!;
    public ICollection<RsvpGuest> Guests { get; set; } = [];
}
