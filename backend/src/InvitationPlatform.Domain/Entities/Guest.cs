using InvitationPlatform.Domain.Enums;

namespace InvitationPlatform.Domain.Entities;

/// <summary>
/// A named invitee on the couple's guest list, with a personal invitation
/// token and per-guest seat allowance. Distinct from RsvpGuest, which stores
/// the individual attendee names submitted with a single RSVP response.
/// </summary>
public class Guest
{
    public Guid Id { get; set; }
    public Guid InvitationId { get; set; }

    public string Name { get; set; } = string.Empty;
    public int MaxAttendees { get; set; } = 1;
    public int SelectedAttendees { get; set; } = 0;
    public GuestRsvpStatus Status { get; set; } = GuestRsvpStatus.Pending;

    /// <summary>URL-safe secure token identifying the guest in their personal link (legacy links).</summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// Name-based, globally-unique URL slug used in the personal link, e.g. "charbel-nahhas".
    /// Uniqueness is enforced in application code (a numeric/random suffix is appended on collision).
    /// </summary>
    public string Slug { get; set; } = string.Empty;

    public DateTime? RespondedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Invitation Invitation { get; set; } = null!;
}
