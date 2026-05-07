using InvitationPlatform.Domain.Enums;

namespace InvitationPlatform.Domain.Entities;

public class Rsvp
{
    public Guid Id { get; set; }
    public Guid InvitationId { get; set; }
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
