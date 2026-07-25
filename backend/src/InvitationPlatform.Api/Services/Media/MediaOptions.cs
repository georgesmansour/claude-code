namespace InvitationPlatform.Api.Services.Media;

/// <summary>Bound from the "Media" configuration section — upload limits and image tuning.</summary>
public class MediaOptions
{
    public long MaxImageBytes { get; set; } = 8L * 1024 * 1024;   // 8 MB
    public long MaxVideoBytes { get; set; } = 15L * 1024 * 1024;  // 15 MB (short decorative clips)
    public long MaxAudioBytes { get; set; } = 10L * 1024 * 1024;  // 10 MB

    /// <summary>Longest edge (px) an uploaded image is downscaled to.</summary>
    public int MaxImageDimension { get; set; } = 2000;

    /// <summary>WebP quality (0-100) used when re-encoding uploaded images.</summary>
    public int WebpQuality { get; set; } = 80;
}
