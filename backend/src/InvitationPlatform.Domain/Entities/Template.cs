namespace InvitationPlatform.Domain.Entities;

public class Template
{
    public Guid Id { get; set; }
    public Guid CreatedBy { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsBuiltin { get; set; } = false;
    public bool IsActive { get; set; } = true;
    public string? ThumbnailUrl { get; set; }

    /// <summary>Full default-data snapshot stored as JSONB.</summary>
    public string Data { get; set; } = "{}";

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation
    public AdminAccount CreatedByAdmin { get; set; } = null!;
    public ICollection<Invitation> Invitations { get; set; } = [];
}
