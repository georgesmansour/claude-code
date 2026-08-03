using InvitationPlatform.Api.Dtos;
using InvitationPlatform.Domain.Entities;

namespace InvitationPlatform.Api.Services;

/// <summary>
/// Converts the single <see cref="LandingSettings"/> row to/from its DTO. Shared by the public
/// read endpoint and the admin editor so both agree on trimming and empty-field handling.
/// </summary>
public static class LandingSettingsMapper
{
    /// <summary>Null/whitespace becomes null so the landing page can simply skip the field.</summary>
    private static string? Clean(string? v) =>
        string.IsNullOrWhiteSpace(v) ? null : v.Trim();

    public static LandingSettingsDto ToDto(LandingSettings? s) => new(
        Clean(s?.CompanyEmail), Clean(s?.PhoneNumber), Clean(s?.WhatsAppNumber), Clean(s?.CompanyAddress),
        Clean(s?.InstagramUrl), Clean(s?.FacebookUrl), Clean(s?.TikTokUrl), Clean(s?.PinterestUrl),
        Clean(s?.MapEmbedUrl));

    public static void Apply(LandingSettings target, LandingSettingsDto dto)
    {
        target.CompanyEmail   = Clean(dto.CompanyEmail);
        target.PhoneNumber    = Clean(dto.PhoneNumber);
        target.WhatsAppNumber = Clean(dto.WhatsAppNumber);
        target.CompanyAddress = Clean(dto.CompanyAddress);
        target.InstagramUrl   = Clean(dto.InstagramUrl);
        target.FacebookUrl    = Clean(dto.FacebookUrl);
        target.TikTokUrl      = Clean(dto.TikTokUrl);
        target.PinterestUrl   = Clean(dto.PinterestUrl);
        target.MapEmbedUrl    = Clean(dto.MapEmbedUrl);
        target.UpdatedAt      = DateTime.UtcNow;
    }
}
