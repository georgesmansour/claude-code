using InvitationPlatform.Domain.Enums;

namespace InvitationPlatform.Api.Services.Media;

/// <summary>
/// Pure, DB-free rules for what may be uploaded. Kept separate so the accept/reject decisions
/// are trivially unit-testable and are the single source of truth for the controller and service.
/// </summary>
public static class MediaValidation
{
    // Explicit allow-lists — never infer "it's fine" from an arbitrary client-supplied MIME type.
    private static readonly Dictionary<string, string> ImageTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = "jpg", ["image/pjpeg"] = "jpg", ["image/png"] = "png",
        ["image/webp"] = "webp", ["image/gif"] = "gif",
    };
    private static readonly Dictionary<string, string> VideoTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["video/mp4"] = "mp4", ["video/webm"] = "webm",
    };
    private static readonly Dictionary<string, string> AudioTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["audio/mpeg"] = "mp3", ["audio/mp3"] = "mp3", ["audio/aac"] = "aac",
        ["audio/ogg"] = "ogg", ["audio/mp4"] = "m4a", ["audio/x-m4a"] = "m4a",
    };

    /// <summary>Resolves the media kind from a whitelisted content type, or null if unsupported.</summary>
    public static MediaKind? ResolveKind(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType)) return null;
        var ct = contentType.Split(';')[0].Trim();
        if (ImageTypes.ContainsKey(ct)) return MediaKind.Image;
        if (VideoTypes.ContainsKey(ct)) return MediaKind.Video;
        if (AudioTypes.ContainsKey(ct)) return MediaKind.Audio;
        return null;
    }

    /// <summary>File extension to store the original (non-image-processed) bytes under.</summary>
    public static string Extension(MediaKind kind, string contentType)
    {
        var ct = contentType.Split(';')[0].Trim();
        return kind switch
        {
            MediaKind.Image => ImageTypes.GetValueOrDefault(ct, "bin"),
            MediaKind.Video => VideoTypes.GetValueOrDefault(ct, "bin"),
            MediaKind.Audio => AudioTypes.GetValueOrDefault(ct, "bin"),
            _ => "bin"
        };
    }

    public static long MaxBytes(MediaKind kind, MediaOptions o) => kind switch
    {
        MediaKind.Image => o.MaxImageBytes,
        MediaKind.Video => o.MaxVideoBytes,
        MediaKind.Audio => o.MaxAudioBytes,
        _ => 0
    };

    /// <summary>Validates kind + size. Returns null when OK, or a user-facing error message.</summary>
    public static string? Validate(string? contentType, long length, MediaOptions o, out MediaKind kind)
    {
        kind = default;
        var resolved = ResolveKind(contentType);
        if (resolved is null)
            return "Unsupported file type. Allowed: JPEG, PNG, WebP, GIF, MP4, WebM, MP3, AAC, OGG, M4A.";
        kind = resolved.Value;

        if (length <= 0) return "The file is empty.";
        var max = MaxBytes(kind, o);
        if (length > max)
            return $"File is too large. Maximum for {kind.ToString().ToLowerInvariant()} is {max / (1024 * 1024)} MB.";
        return null;
    }
}
