namespace InvitationPlatform.Api.Services.Media;

/// <summary>A user-facing media error (bad type, too big, unreadable image). Maps to HTTP 400.</summary>
public class MediaException(string message) : Exception(message);
