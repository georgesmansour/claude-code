using Microsoft.Extensions.Options;

namespace InvitationPlatform.Api.Services.Storage;

/// <summary>
/// Stores files on the local filesystem under a configurable base directory. Works identically
/// in local development and in production as long as the base path points at a persistent volume.
/// </summary>
public class LocalFileStorage : IFileStorage
{
    private readonly string _root;
    private readonly ILogger<LocalFileStorage> _log;

    public LocalFileStorage(IOptions<StorageOptions> options, IHostEnvironment env, ILogger<LocalFileStorage> log)
    {
        _log = log;
        var configured = options.Value.BasePath;
        _root = Path.IsPathRooted(configured)
            ? configured
            : Path.Combine(env.ContentRootPath, configured);
        Directory.CreateDirectory(_root);
        _log.LogInformation("User-media storage root: {Root}", _root);
    }

    // Resolve a provider-relative path to a full path and refuse anything that escapes the root
    // (path-traversal guard). relativePath is app-generated, but we validate defensively.
    private string Resolve(string relativePath)
    {
        var full = Path.GetFullPath(Path.Combine(_root, relativePath));
        var rootWithSep = _root.EndsWith(Path.DirectorySeparatorChar)
            ? _root : _root + Path.DirectorySeparatorChar;
        if (!full.StartsWith(rootWithSep, StringComparison.Ordinal))
            throw new InvalidOperationException("Resolved path escapes the storage root.");
        return full;
    }

    public async Task SaveAsync(string relativePath, Stream content, CancellationToken ct = default)
    {
        var full = Resolve(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await using var fs = new FileStream(full, FileMode.Create, FileAccess.Write, FileShare.None);
        await content.CopyToAsync(fs, ct);
    }

    public Task<Stream?> OpenReadAsync(string relativePath, CancellationToken ct = default)
    {
        var full = Resolve(relativePath);
        if (!File.Exists(full)) return Task.FromResult<Stream?>(null);
        Stream s = new FileStream(full, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult<Stream?>(s);
    }

    public Task<bool> DeleteAsync(string relativePath, CancellationToken ct = default)
    {
        var full = Resolve(relativePath);
        if (!File.Exists(full)) return Task.FromResult(false);
        File.Delete(full);
        return Task.FromResult(true);
    }

    public Task<bool> ExistsAsync(string relativePath, CancellationToken ct = default)
        => Task.FromResult(File.Exists(Resolve(relativePath)));
}
