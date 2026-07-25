using InvitationPlatform.Domain.Enums;

namespace InvitationPlatform.Domain.Entities;

/// <summary>
/// A media file uploaded by an invitation creator (photo, video or audio clip).
///
/// This is deliberately SEPARATE from built-in template assets: template images live
/// in the templates' JSON as application-owned URLs and are never represented here, so
/// a user can never delete, replace or overwrite them. Every row in this table is
/// owned by exactly one invitation and is fully editable/deletable by its owner.
/// </summary>
public class UserMedia
{
    public Guid Id { get; set; }

    /// <summary>Owning invitation. Media is deleted when the invitation is deleted.</summary>
    public Guid InvitationId { get; set; }

    public MediaKind Kind { get; set; }

    /// <summary>Original client-supplied file name (display only, never used for storage).</summary>
    public string OriginalFileName { get; set; } = string.Empty;

    /// <summary>Content-addressed file name actually written to storage (hash + extension).</summary>
    public string StoredFileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;
    public long ByteSize { get; set; }

    /// <summary>SHA-256 (hex) of the stored bytes — used to detect duplicate uploads.</summary>
    public string ContentHash { get; set; } = string.Empty;

    /// <summary>Pixel dimensions for images/videos; null for audio.</summary>
    public int? Width { get; set; }
    public int? Height { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Invitation Invitation { get; set; } = null!;
}
