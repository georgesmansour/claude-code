using InvitationPlatform.Domain.Enums;

namespace InvitationPlatform.Domain.Entities;

public class Invitation
{
    public Guid Id { get; set; }
    public Guid CreatedBy { get; set; }
    public Guid? TemplateId { get; set; }

    /// <summary>URL-friendly key used in ?id= parameter.</summary>
    public string Slug { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public InvitationStatus Status { get; set; } = InvitationStatus.Draft;

    /// <summary>Random hex token used in portable share links.</summary>
    public string PublicToken { get; set; } = string.Empty;
    public string? PasswordHash { get; set; }

    public string? EventType { get; set; }
    public DateTime? EventDate { get; set; }
    public int MaxAttendees { get; set; } = 10;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? PublishedAt { get; set; }

    // Navigation
    public AdminAccount CreatedByAdmin { get; set; } = null!;
    public Template? Template { get; set; }
    public ClientAccount? Client { get; set; }
    public ICollection<InvitationSection> Sections { get; set; } = [];
    public ICollection<Rsvp> Rsvps { get; set; } = [];
    public ICollection<AuditLog> AuditLogs { get; set; } = [];
}
