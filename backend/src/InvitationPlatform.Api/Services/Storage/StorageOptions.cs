namespace InvitationPlatform.Api.Services.Storage;

/// <summary>Bound from the "Storage" configuration section.</summary>
public class StorageOptions
{
    /// <summary>
    /// Root directory for user media. Relative paths are resolved against the app content root
    /// (good for local dev); set an absolute path to a persistent volume in production.
    /// </summary>
    public string BasePath { get; set; } = "media_storage";
}
