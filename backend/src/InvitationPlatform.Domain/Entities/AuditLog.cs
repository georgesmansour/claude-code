namespace InvitationPlatform.Domain.Entities;

public class AuditLog
{
    public long Id { get; set; }
    public Guid? AdminId { get; set; }
    public Guid? InvitationId { get; set; }

    /// <summary>create | update | delete | publish | login</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>invitation | client | template | rsvp</summary>
    public string? EntityType { get; set; }
    public Guid? EntityId { get; set; }

    /// <summary>Snapshot of changed fields stored as JSONB.</summary>
    public string? Details { get; set; }
    public string? IpAddress { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigation
    public AdminAccount? Admin { get; set; }
    public Invitation? Invitation { get; set; }
}
