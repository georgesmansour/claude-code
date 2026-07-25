namespace InvitationPlatform.Api.Services.Storage;

/// <summary>
/// Abstraction over where user-uploaded files physically live. The application only ever
/// deals in provider-relative paths (e.g. "&lt;invitationId&gt;/&lt;hash&gt;.webp"), so swapping the
/// local-disk implementation for Azure Blob / S3 later needs no changes outside this folder.
/// </summary>
public interface IFileStorage
{
    /// <summary>Writes (or overwrites) the file at <paramref name="relativePath"/>.</summary>
    Task SaveAsync(string relativePath, Stream content, CancellationToken ct = default);

    /// <summary>Opens the file for reading, or returns null if it does not exist.</summary>
    Task<Stream?> OpenReadAsync(string relativePath, CancellationToken ct = default);

    /// <summary>Deletes the file; returns false if it was not there.</summary>
    Task<bool> DeleteAsync(string relativePath, CancellationToken ct = default);

    Task<bool> ExistsAsync(string relativePath, CancellationToken ct = default);
}
