namespace InvitationPlatform.Api.Dtos;

// ── AUTH ────────────────────────────────────────────────────
public record LoginRequest(string Email, string Password);
public record LoginResponse(string Token, string Role, string FullName, bool MustChangePassword);
public record ChangePasswordRequest(string? CurrentPassword, string NewPassword);

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
    public string? Name { get; set; }
    public string? Addr { get; set; }
    public string? Url { get; set; }
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
}

public class CustomSection
{
    public bool Enabled { get; set; } = true;
    public string? Label { get; set; }
    public string? Title { get; set; }
    public string? Body { get; set; }
    public string? Image { get; set; }
}

// ── INVITATION CRUD ────────────────────────────────────────
public record InvitationListItem(
    Guid Id, string Slug, string Title, string Status,
    DateTime? EventDate, int RsvpCount, DateTime UpdatedAt);

public record InvitationFull(
    Guid Id, string Slug, string Title, string Status,
    string? EventType, DateTime? EventDate, int MaxAttendees,
    Guid? TemplateId, InvitationData Data);

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
    string? Message, List<RsvpGuestRequest> Guests);

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

// ── DASHBOARD ──────────────────────────────────────────────
public record DashboardSummary(
    Guid InvitationId, string Slug, string Title,
    DateTime? EventDate, int MaxAttendees,
    int TotalRsvps, int Attending, int Declined,
    int Maybe, int TotalSeats, double AcceptRate);
