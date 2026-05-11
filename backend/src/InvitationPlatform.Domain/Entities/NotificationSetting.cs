namespace InvitationPlatform.Domain.Entities;

public class NotificationSetting
{
    public Guid Id { get; set; }
    public Guid ClientId { get; set; }

    public bool EmailOnRsvp { get; set; } = true;
    public bool SmsOnRsvp { get; set; } = false;
    public bool DailySummary { get; set; } = false;

    // Navigation
    public ClientAccount Client { get; set; } = null!;
}
