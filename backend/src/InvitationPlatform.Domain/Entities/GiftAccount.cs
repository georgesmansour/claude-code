namespace InvitationPlatform.Domain.Entities;

public class GiftAccount
{
    public Guid Id { get; set; }
    public Guid SectionId { get; set; }
    public int OrderIndex { get; set; } = 0;

    public string? BankName { get; set; }
    public string AccountNumber { get; set; } = string.Empty;

    /// <summary>iban | mobile | card | paypal | other</summary>
    public string? AccountKind { get; set; }
    public string? Notes { get; set; }

    // Navigation
    public InvitationSection Section { get; set; } = null!;
}
