namespace InvitationPlatform.Api.Dtos;

// ── AUTH ────────────────────────────────────────────────────
public record LoginRequest(string Email, string Password);
public record LoginResponse(string Token, string Role, string FullName, bool MustChangePassword);
public record ChangePasswordRequest(string? CurrentPassword, string NewPassword);
public record ChangeEmailRequest(string CurrentPassword, string NewEmail);
public record UpdateClientCredentialsRequest(string? NewEmail, string? NewPassword);

// ── INVITATION DATA SHAPE (matches the existing JS data model) ──
public class InvitationData
{
    public string? Title { get; set; }
    public CoverData? Cover { get; set; }
    public CountdownData? Countdown { get; set; }
    public LocationsData? Locations { get; set; }
    public GiftsData? Gifts { get; set; }
    public RsvpData? Rsvp { get; set; }
    public List<CustomSection>? CustomSections { get; set; }
    public GalleryData? Gallery { get; set; }
    public TimelineData? Timeline { get; set; }
    public FamiliesData? Families { get; set; }
    public MemoriesData? Memories { get; set; }
    public MusicData? Music { get; set; }
}

public class CoverData
{
    public bool Enabled { get; set; } = true;
    public string? EventLabel { get; set; }
    public string? Names { get; set; }
    public string? Tagline { get; set; }
    public string? HostText { get; set; }
    public string? HostIntro { get; set; }
    public string? HostOutro { get; set; }
    public string? Image { get; set; }
    public string? ButtonText { get; set; }
    // Elegant Noir envelope screen: "Dear {guest}" prefix + wax-seal image
    public string? Greeting { get; set; }
    public string? SealImage { get; set; }
}

public class CountdownData
{
    public bool Enabled { get; set; } = true;
    public string? Label { get; set; }
    public string? Date { get; set; }
    public string? Description { get; set; }
    public string? Image { get; set; }
}

public class LocationsData
{
    public bool Enabled { get; set; } = true;
    public string? Label { get; set; }
    public string? Title { get; set; }
    public string? Image { get; set; }
    public List<LocationItem> Items { get; set; } = [];
}

public class LocationItem
{
    public string? Time { get; set; }
    public string? Label { get; set; }
    public string? Name { get; set; }
    public string? Addr { get; set; }
    public string? Url { get; set; }
    public string? Img { get; set; }
}

public class GiftsData
{
    public bool Enabled { get; set; } = true;
    public string? Label { get; set; }
    public string? Title { get; set; }
    public string? Image { get; set; }
    public string? Description { get; set; }
    public List<GiftItem> Items { get; set; } = [];
}

public class GiftItem
{
    public string? Bank { get; set; }
    public string? Account { get; set; }
}

public class RsvpData
{
    public bool Enabled { get; set; } = true;
    public string? Label { get; set; }
    public string? Title { get; set; }
    public string? Image { get; set; }
    public string? Deadline { get; set; }
    public int MaxPeople { get; set; } = 10;
    public string? ButtonText { get; set; }
    public bool AllowWishes { get; set; } = true;
}

public class CustomSection
{
    public bool Enabled { get; set; } = true;
    public string? Label { get; set; }
    public string? Title { get; set; }
    public string? Body { get; set; }
    public string? Image { get; set; }
}

public class GalleryData
{
    public bool Enabled { get; set; } = true;
    public string? Label { get; set; }
    public string? Title { get; set; }
    public List<GalleryImage> Items { get; set; } = [];
}

public class GalleryImage
{
    public string? Url { get; set; }
    public string? Caption { get; set; }
}

public class TimelineData
{
    public bool Enabled { get; set; } = true;
    public string? Label { get; set; }
    public string? Title { get; set; }
    public List<TimelineItem> Items { get; set; } = [];
}

public class TimelineItem
{
    public string? Time { get; set; }
    public string? Title { get; set; }
    public string? Subtitle { get; set; }
    public string? Icon { get; set; }
}

public class FamiliesData
{
    public bool Enabled { get; set; } = true;
    public string? Label { get; set; }
    public string? Title { get; set; }
    public List<FamilyItem> Items { get; set; } = [];
}

public class FamilyItem
{
    public string? Label { get; set; }
    public string? Names { get; set; }
}

public class MemoriesData
{
    public bool Enabled { get; set; } = true;
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? Url { get; set; }
    public string? ButtonText { get; set; }
}

public class MusicData
{
    public bool Enabled { get; set; } = true;
    public string? Url { get; set; }
    public bool Autoplay { get; set; } = true;
}

// ── INVITATION CRUD ────────────────────────────────────────
public record InvitationListItem(
    Guid Id, string Slug, string Title, string Status,
    DateTime? EventDate, int RsvpCount, DateTime UpdatedAt);

public record InvitationFull(
    Guid Id, string Slug, string Title, string Status,
    string? EventType, DateTime? EventDate, int MaxAttendees,
    Guid? TemplateId, DateTime UpdatedAt, InvitationData Data,
    string? PublicToken = null);

public record CreateInvitationRequest(
    string Title, string Slug, Guid? TemplateId,
    string? EventType, DateTime? EventDate, InvitationData Data);

public record UpdateInvitationRequest(
    string Title, string Slug, string? EventType,
    DateTime? EventDate, int MaxAttendees, InvitationData Data);

// ── TEMPLATE ───────────────────────────────────────────────
public record TemplateDto(
    Guid Id, string Name, string? Description,
    bool IsBuiltin, bool IsActive, InvitationData Data);

public record CreateTemplateRequest(string Name, string? Description, InvitationData Data);
public record UpdateTemplateRequest(string Name, string? Description, bool IsActive, InvitationData Data);

// ── CLIENT ACCOUNT ─────────────────────────────────────────
public record CreateClientRequest(
    Guid InvitationId, string Email, string Password,
    string FullName, string? Phone);

public record ClientAccountDto(
    Guid Id, Guid InvitationId, string Email, string FullName,
    string? Phone, bool IsActive, bool MustChangePassword,
    DateTime? LastLoginAt, DateTime CreatedAt);

// ── RSVP ───────────────────────────────────────────────────
public record SubmitRsvpRequest(
    string Response, int PartySize,
    string? ContactName, string? ContactEmail, string? ContactPhone,
    string? Message, List<RsvpGuestRequest> Guests,
    string? GuestToken = null);

public record RsvpGuestRequest(
    string FullName, string? AgeGroup,
    string? MealPreference, string? DietaryRestrictions);

public record RsvpDto(
    Guid Id, string Response, int PartySize,
    string? ContactName, string? ContactEmail, string? ContactPhone,
    string? Message, DateTime CreatedAt,
    List<RsvpGuestDto> Guests);

public record RsvpGuestDto(
    string FullName, string? AgeGroup,
    string? MealPreference, string? DietaryRestrictions);

// ── GUEST LIST (Bride & Groom dashboard) ───────────────────
public record GuestDto(
    Guid Id, string Name, int MaxAttendees, int SelectedAttendees,
    string Status, string Token, string Slug, DateTime? RespondedAt, DateTime UpdatedAt);

public record ImportGuestRow(int Row, string? Name, int? MaxAttendees);
public record ImportGuestsRequest(List<ImportGuestRow> Rows);
public record ImportRowError(int Row, string Reason);
public record ImportGuestsResult(int Created, int Updated, List<ImportRowError> Failed);

public record CreateGuestRequest(string Name, int MaxAttendees);
public record UpdateGuestRequest(string Name, int MaxAttendees);

// ── CLIENT SELF-EDIT ───────────────────────────────────────
public record ClientUpdateInvitationRequest(string Title, InvitationData Data);

// ── DASHBOARD ──────────────────────────────────────────────
public record DashboardSummary(
    Guid InvitationId, string Slug, string Title,
    DateTime? EventDate, int MaxAttendees,
    int TotalRsvps, int Attending, int Declined,
    int TotalSeats, double AcceptRate);
