namespace InvitationPlatform.Api.Dtos;

/// <summary>A user-uploaded media file, as returned to the client.</summary>
public record MediaDto(
    Guid Id,
    string Kind,
    string Url,
    string ContentType,
    long ByteSize,
    int? Width,
    int? Height,
    string OriginalFileName,
    DateTime CreatedAt);
