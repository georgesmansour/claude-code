namespace InvitationPlatform.Domain.Entities;

/// <summary>
/// Publicly-visible contact details shown on the landing page, editable from the admin panel.
///
/// This is a single-row table. It is deliberately SEPARATE from <c>system_settings</c> (which holds
/// the JWT signing key): everything here is served to anonymous visitors, so keeping it in its own
/// table makes it impossible for a public endpoint to accidentally expose a secret.
///
/// Every field is optional — the landing page simply omits whatever is blank.
/// </summary>
public class LandingSettings
{
    public Guid Id { get; set; }

    public string? CompanyEmail { get; set; }
    public string? PhoneNumber { get; set; }
    /// <summary>Digits/plus only; rendered as a wa.me link.</summary>
    public string? WhatsAppNumber { get; set; }
    public string? CompanyAddress { get; set; }

    public string? InstagramUrl { get; set; }
    public string? FacebookUrl { get; set; }
    public string? TikTokUrl { get; set; }
    public string? PinterestUrl { get; set; }

    /// <summary>Optional Google Maps embed URL for the contact section.</summary>
    public string? MapEmbedUrl { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
