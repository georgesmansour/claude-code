namespace InvitationPlatform.Domain.Entities;

public class AdminAccount
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Highest privilege level. Only Super Admins may access the /admin section; a regular
    /// admin cannot grant themselves this flag through the API, preventing privilege escalation.
    /// </summary>
    public bool IsSuperAdmin { get; set; } = false;
    public DateTime? LastLoginAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation
    public ICollection<Template> Templates { get; set; } = [];
    public ICollection<Invitation> Invitations { get; set; } = [];
    public ICollection<ClientAccount> ClientAccounts { get; set; } = [];
    public ICollection<AuditLog> AuditLogs { get; set; } = [];
}
