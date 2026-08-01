namespace InvitationPlatform.Domain.Entities;

public class ClientAccount
{
    public Guid Id { get; set; }

    /// <summary>Each client is bound to exactly one invitation.</summary>
    public Guid InvitationId { get; set; }
    public Guid CreatedBy { get; set; }

    /// <summary>
    /// Contact email. Nullable because a client may register with a phone number instead — but a
    /// DB check constraint guarantees at least one of <see cref="Email"/>/<see cref="Phone"/> is set.
    /// </summary>
    public string? Email { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Phone { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>Forces password change on first login.</summary>
    public bool MustChangePassword { get; set; } = true;

    public DateTime? LastLoginAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation
    public Invitation Invitation { get; set; } = null!;
    public AdminAccount CreatedByAdmin { get; set; } = null!;
    public NotificationSetting? NotificationSetting { get; set; }
}
