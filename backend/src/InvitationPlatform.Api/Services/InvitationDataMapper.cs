using System.Text.Json;
using InvitationPlatform.Api.Dtos;
using InvitationPlatform.Domain.Entities;
using InvitationPlatform.Domain.Enums;

namespace InvitationPlatform.Api.Services;

/// <summary>
/// Translates between the relational tables (Invitation + Sections + Locations + GiftAccounts)
/// and the unified <see cref="InvitationData"/> shape consumed by the front-end.
/// </summary>
public static class InvitationDataMapper
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public static InvitationData ToData(Invitation inv)
    {
        var data = new InvitationData { Title = inv.Title };

        foreach (var s in inv.Sections.OrderBy(x => x.OrderIndex))
        {
            switch (s.Type)
            {
                case SectionType.Cover:      data.Cover     = Deserialize<CoverData>(s.Config);     SetEnabled(data.Cover,     s.Enabled); break;
                case SectionType.Countdown:  data.Countdown = Deserialize<CountdownData>(s.Config); SetEnabled(data.Countdown, s.Enabled); break;
                case SectionType.Locations:
                    data.Locations = Deserialize<LocationsData>(s.Config) ?? new();
                    SetEnabled(data.Locations, s.Enabled);
                    data.Locations.Items = s.Locations.OrderBy(l => l.OrderIndex)
                        .Select(l => new LocationItem { Time = l.TimeLabel, Label = l.Label, Name = l.Name, Addr = l.Address, Url = l.MapUrl, Img = l.ImageUrl })
                        .ToList();
                    break;
                case SectionType.Gifts:
                    data.Gifts = Deserialize<GiftsData>(s.Config) ?? new();
                    SetEnabled(data.Gifts, s.Enabled);
                    data.Gifts.Items = s.GiftAccounts.OrderBy(g => g.OrderIndex)
                        .Select(g => new GiftItem { Bank = g.BankName, Account = g.AccountNumber })
                        .ToList();
                    break;
                case SectionType.Rsvp:
                    data.Rsvp = Deserialize<RsvpData>(s.Config) ?? new();
                    SetEnabled(data.Rsvp, s.Enabled);
                    data.Rsvp.MaxPeople = inv.MaxAttendees;
                    break;
                case SectionType.Custom:
                    data.CustomSections ??= [];
                    var custom = Deserialize<CustomSection>(s.Config) ?? new();
                    custom.Enabled = s.Enabled;
                    data.CustomSections.Add(custom);
                    break;
                case SectionType.Gallery:  data.Gallery  = Deserialize<GalleryData>(s.Config);  SetEnabled(data.Gallery,  s.Enabled); break;
                case SectionType.Timeline: data.Timeline = Deserialize<TimelineData>(s.Config); SetEnabled(data.Timeline, s.Enabled); break;
                case SectionType.Families: data.Families = Deserialize<FamiliesData>(s.Config); SetEnabled(data.Families, s.Enabled); break;
                case SectionType.Memories: data.Memories = Deserialize<MemoriesData>(s.Config); SetEnabled(data.Memories, s.Enabled); break;
                case SectionType.Music:    data.Music    = Deserialize<MusicData>(s.Config);    SetEnabled(data.Music,    s.Enabled); break;
            }
        }
        return data;
    }

    /// <summary>Replaces the sections of an invitation with rows reconstructed from the DTO.</summary>
    public static void ApplyData(Invitation inv, InvitationData data)
    {
        inv.Sections.Clear();

        var order = 0;
        if (data.Cover     is not null) inv.Sections.Add(BuildSection(SectionType.Cover,     data.Cover,     data.Cover.Enabled,     order++));
        if (data.Countdown is not null) inv.Sections.Add(BuildSection(SectionType.Countdown, data.Countdown, data.Countdown.Enabled, order++));

        if (data.Locations is not null)
        {
            var sec = BuildSection(SectionType.Locations, new LocationsConfig(data.Locations), data.Locations.Enabled, order++);
            foreach (var (loc, i) in data.Locations.Items.Select((l, i) => (l, i)))
            {
                sec.Locations.Add(new Location
                {
                    OrderIndex = i,
                    TimeLabel = loc.Time,
                    Label = loc.Label,
                    Name = loc.Name ?? "",
                    Address = loc.Addr,
                    MapUrl = loc.Url,
                    ImageUrl = loc.Img
                });
            }
            inv.Sections.Add(sec);
        }

        if (data.Gifts is not null)
        {
            var sec = BuildSection(SectionType.Gifts, new GiftsConfig(data.Gifts), data.Gifts.Enabled, order++);
            foreach (var (g, i) in data.Gifts.Items.Select((g, i) => (g, i)))
            {
                sec.GiftAccounts.Add(new GiftAccount
                {
                    OrderIndex = i,
                    BankName = g.Bank,
                    AccountNumber = g.Account ?? ""
                });
            }
            inv.Sections.Add(sec);
        }

        foreach (var c in data.CustomSections ?? [])
            inv.Sections.Add(BuildSection(SectionType.Custom, c, c.Enabled, order++));

        if (data.Gallery  is not null) inv.Sections.Add(BuildSection(SectionType.Gallery,  data.Gallery,  data.Gallery.Enabled,  order++));
        if (data.Timeline is not null) inv.Sections.Add(BuildSection(SectionType.Timeline, data.Timeline, data.Timeline.Enabled, order++));
        if (data.Families is not null) inv.Sections.Add(BuildSection(SectionType.Families, data.Families, data.Families.Enabled, order++));
        if (data.Memories is not null) inv.Sections.Add(BuildSection(SectionType.Memories, data.Memories, data.Memories.Enabled, order++));
        if (data.Music    is not null) inv.Sections.Add(BuildSection(SectionType.Music,    data.Music,    data.Music.Enabled,    order++));

        if (data.Rsvp is not null)
        {
            inv.MaxAttendees = data.Rsvp.MaxPeople;
            inv.Sections.Add(BuildSection(SectionType.Rsvp, data.Rsvp, data.Rsvp.Enabled, order++));
        }
    }

    private static InvitationSection BuildSection(SectionType type, object config, bool enabled, int order) => new()
    {
        Type = type,
        Enabled = enabled,
        OrderIndex = order,
        Config = JsonSerializer.Serialize(config, Json)
    };

    private static T? Deserialize<T>(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return default;
        try { return JsonSerializer.Deserialize<T>(json, Json); }
        catch { return default; }
    }

    private static void SetEnabled(object? section, bool enabled)
    {
        if (section is null) return;
        var prop = section.GetType().GetProperty("Enabled");
        prop?.SetValue(section, enabled);
    }

    // strip out Items so they don't double-store in jsonb config
    private record LocationsConfig(string? Label, string? Title, string? Image)
    {
        public LocationsConfig(LocationsData d) : this(d.Label, d.Title, d.Image) { }
    }
    private record GiftsConfig(string? Label, string? Title, string? Image, string? Description)
    {
        public GiftsConfig(GiftsData d) : this(d.Label, d.Title, d.Image, d.Description) { }
    }
}
