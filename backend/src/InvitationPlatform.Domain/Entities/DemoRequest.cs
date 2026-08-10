namespace InvitationPlatform.Domain.Entities;

/// <summary>
/// A "Request a Demo" enquiry submitted from the public landing page.
///
/// Every enquiry is persisted BEFORE the notification email is attempted, so a misconfigured or
/// temporarily unavailable SMTP server can never lose a lead — the admin can still read it from
/// the database. <see cref="EmailSentAt"/> records whether the notification actually went out.
/// </summary>
public class DemoRequest
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? EventType { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Company { get; set; }
    public string? Message { get; set; }

    /// <summary>
    /// When the Super Admin opened this enquiry in the admin panel. Null = unread, which is what
    /// drives the in-app notification badge — the primary way we surface new leads, so no email
    /// configuration is required for the admin to be notified.
    /// </summary>
    public DateTime? ReadAt { get; set; }

    /// <summary>Set once the notification email has been delivered to the configured address.</summary>
    public DateTime? EmailSentAt { get; set; }
    /// <summary>Populated when sending failed, so the cause is visible without digging in logs.</summary>
    public string? EmailError { get; set; }

    public string? IpAddress { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
