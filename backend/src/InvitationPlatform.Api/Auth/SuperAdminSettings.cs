namespace InvitationPlatform.Api.Auth;

/// <summary>
/// Super Admin bootstrap credentials, bound from the "SuperAdmin" configuration section via the
/// Options pattern. Kept out of source so the account is configured, not hardcoded. In production,
/// override the password with an environment variable or user-secrets rather than committing it.
/// </summary>
public class SuperAdminSettings
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FirstName { get; set; } = "Super";
    public string LastName { get; set; } = "Admin";

    /// <summary>Combined display name used for the <c>AdminAccount.FullName</c> column.</summary>
    public string FullName => $"{FirstName} {LastName}".Trim();
}
