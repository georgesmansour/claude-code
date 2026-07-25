using System.Security.Cryptography;
using InvitationPlatform.Api.Dtos;
using InvitationPlatform.Api.Services.Storage;
using InvitationPlatform.Domain.Entities;
using InvitationPlatform.Domain.Enums;
using InvitationPlatform.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace InvitationPlatform.Api.Services.Media;

/// <summary>
/// Orchestrates user-media uploads: validation, image optimisation, content-hash de-duplication,
/// physical storage (via <see cref="IFileStorage"/>) and the database record. Built-in template
/// assets are never touched here — this service only ever creates rows in <c>user_media</c>.
/// </summary>
public class MediaService(
    AppDbContext db,
    IFileStorage storage,
    IOptions<MediaOptions> options,
    ILogger<MediaService> log)
{
    private readonly MediaOptions _opt = options.Value;

    public static string UrlFor(Guid mediaId) => $"/api/public/media/{mediaId}";
    private static string RelativePath(Guid invitationId, string storedFileName) => $"{invitationId}/{storedFileName}";

    private static MediaDto ToDto(UserMedia m) => new(
        m.Id, m.Kind.ToString(), UrlFor(m.Id), m.ContentType,
        m.ByteSize, m.Width, m.Height, m.OriginalFileName, m.CreatedAt);

    public async Task<List<MediaDto>> ListAsync(Guid invitationId, CancellationToken ct = default)
    {
        var rows = await db.UserMedia
            .Where(m => m.InvitationId == invitationId)
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync(ct);
        return rows.Select(ToDto).ToList();
    }

    /// <summary>Validates, optimises, de-duplicates and stores an upload for one invitation.</summary>
    public async Task<MediaDto> UploadAsync(
        Guid invitationId, Stream content, string fileName, string contentType, long length,
        CancellationToken ct = default)
    {
        var error = MediaValidation.Validate(contentType, length, _opt, out var kind);
        if (error is not null) throw new MediaException(error);

        // Buffer the (size-capped) upload so we can hash + optimise it.
        byte[] bytes;
        await using (var ms = new MemoryStream())
        {
            await content.CopyToAsync(ms, ct);
            bytes = ms.ToArray();
        }
        if (bytes.Length == 0) throw new MediaException("The file is empty.");

        string storedContentType = contentType.Split(';')[0].Trim();
        string ext = MediaValidation.Extension(kind, storedContentType);
        int? width = null, height = null;

        if (kind == MediaKind.Image && !storedContentType.Equals("image/gif", StringComparison.OrdinalIgnoreCase))
        {
            // Re-encode photos to size-capped WebP: smaller payloads, strips metadata, and
            // neutralises anything malicious masquerading as an image.
            try
            {
                using var image = Image.Load(bytes);
                if (Math.Max(image.Width, image.Height) > _opt.MaxImageDimension)
                {
                    image.Mutate(x => x.Resize(new ResizeOptions
                    {
                        Mode = ResizeMode.Max,
                        Size = new Size(_opt.MaxImageDimension, _opt.MaxImageDimension)
                    }));
                }
                width = image.Width;
                height = image.Height;
                await using var outMs = new MemoryStream();
                await image.SaveAsWebpAsync(outMs, new WebpEncoder { Quality = _opt.WebpQuality }, ct);
                bytes = outMs.ToArray();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                throw new MediaException("The image could not be read. Please upload a valid image file.");
            }
            storedContentType = "image/webp";
            ext = "webp";
        }
        else if (kind == MediaKind.Image) // animated GIF kept as-is; just read its dimensions
        {
            try { var info = Image.Identify(bytes); width = info.Width; height = info.Height; }
            catch { /* dimensions are best-effort */ }
        }

        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

        // De-duplicate: same bytes already uploaded for this invitation → reuse the row.
        var existing = await db.UserMedia
            .FirstOrDefaultAsync(m => m.InvitationId == invitationId && m.ContentHash == hash, ct);
        if (existing is not null)
        {
            var relExisting = RelativePath(invitationId, existing.StoredFileName);
            if (!await storage.ExistsAsync(relExisting, ct))
                await storage.SaveAsync(relExisting, new MemoryStream(bytes), ct); // heal a missing file
            log.LogInformation("Media dedup hit for invitation {Invitation} (hash {Hash})", invitationId, hash);
            return ToDto(existing);
        }

        var storedFileName = $"{hash}.{ext}";
        await storage.SaveAsync(RelativePath(invitationId, storedFileName), new MemoryStream(bytes), ct);

        var media = new UserMedia
        {
            InvitationId = invitationId,
            Kind = kind,
            OriginalFileName = string.IsNullOrWhiteSpace(fileName) ? storedFileName : Path.GetFileName(fileName),
            StoredFileName = storedFileName,
            ContentType = storedContentType,
            ByteSize = bytes.Length,
            ContentHash = hash,
            Width = width,
            Height = height
        };
        db.UserMedia.Add(media);
        await db.SaveChangesAsync(ct);
        log.LogInformation("Stored {Kind} media {Id} ({Bytes} bytes) for invitation {Invitation}",
            kind, media.Id, media.ByteSize, invitationId);
        return ToDto(media);
    }

    public async Task<bool> DeleteAsync(Guid invitationId, Guid mediaId, CancellationToken ct = default)
    {
        var media = await db.UserMedia
            .FirstOrDefaultAsync(m => m.Id == mediaId && m.InvitationId == invitationId, ct);
        if (media is null) return false;

        // 1 row ↔ 1 physical file (paths are namespaced per invitation + hash), so deleting is safe.
        await storage.DeleteAsync(RelativePath(invitationId, media.StoredFileName), ct);
        db.UserMedia.Remove(media);
        await db.SaveChangesAsync(ct);
        log.LogInformation("Deleted media {Id} from invitation {Invitation}", mediaId, invitationId);
        return true;
    }

    /// <summary>Opens a media file for the public streaming endpoint. Null if it does not exist.</summary>
    public async Task<(UserMedia Meta, Stream Content)?> OpenAsync(Guid mediaId, CancellationToken ct = default)
    {
        var media = await db.UserMedia.FirstOrDefaultAsync(m => m.Id == mediaId, ct);
        if (media is null) return null;
        var stream = await storage.OpenReadAsync(RelativePath(media.InvitationId, media.StoredFileName), ct);
        return stream is null ? null : (media, stream);
    }
}
