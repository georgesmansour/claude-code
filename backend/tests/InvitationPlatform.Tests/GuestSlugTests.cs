using InvitationPlatform.Api.Dtos;
using Microsoft.EntityFrameworkCore;

namespace InvitationPlatform.Tests;

/// <summary>
/// Bug #2 — the personal URL is name-based, so renaming a guest must regenerate the slug,
/// keep it globally unique, and leave cosmetic-only edits untouched.
/// </summary>
public class GuestSlugTests
{
    [Fact]
    public async Task Renaming_guest_regenerates_the_slug()
    {
        using var db = TestSupport.NewDb();
        var inv = TestSupport.SeedInvitation(db);
        var guest = TestSupport.SeedGuest(db, inv.Id, "John Doe", "john-doe");
        var ctrl = TestSupport.NewClientController(db, inv.Id);

        var result = await ctrl.UpdateGuest(guest.Id, new UpdateGuestRequest("Jane Doe", 4));

        var dto = TestSupport.Body<GuestDto>(result);
        Assert.Equal("Jane Doe", dto.Name);
        Assert.Equal("jane-doe", dto.Slug);

        var reloaded = await db.Guests.SingleAsync(g => g.Id == guest.Id);
        Assert.Equal("jane-doe", reloaded.Slug);
    }

    [Fact]
    public async Task Renaming_appends_suffix_when_new_slug_is_taken_globally()
    {
        using var db = TestSupport.NewDb();
        var inv = TestSupport.SeedInvitation(db, "wedding-a");
        // A guest on a DIFFERENT invitation already owns the "jane-doe" slug. Names are unique
        // per-invitation, but slugs are globally unique, so the rename must fall back to a suffix.
        var otherInv = TestSupport.SeedInvitation(db, "wedding-b");
        TestSupport.SeedGuest(db, otherInv.Id, "Jane Doe", "jane-doe", token: "tok-jane");
        var guest = TestSupport.SeedGuest(db, inv.Id, "John Doe", "john-doe", token: "tok-john");
        var ctrl = TestSupport.NewClientController(db, inv.Id);

        var result = await ctrl.UpdateGuest(guest.Id, new UpdateGuestRequest("Jane Doe", 4));

        var dto = TestSupport.Body<GuestDto>(result);
        Assert.Equal("jane-doe-2", dto.Slug);
    }

    [Fact]
    public async Task Cosmetic_rename_keeps_the_existing_slug_stable()
    {
        using var db = TestSupport.NewDb();
        var inv = TestSupport.SeedInvitation(db);
        var guest = TestSupport.SeedGuest(db, inv.Id, "John Doe", "john-doe-7"); // deliberately non-default slug
        var ctrl = TestSupport.NewClientController(db, inv.Id);

        // Only whitespace/case differs → same base slug → link must not churn.
        var result = await ctrl.UpdateGuest(guest.Id, new UpdateGuestRequest("  john   DOE ", 6));

        var dto = TestSupport.Body<GuestDto>(result);
        Assert.Equal("john-doe-7", dto.Slug);
        Assert.Equal(6, dto.MaxAttendees);
    }

    [Fact]
    public async Task Renaming_with_accents_produces_ascii_slug()
    {
        using var db = TestSupport.NewDb();
        var inv = TestSupport.SeedInvitation(db);
        var guest = TestSupport.SeedGuest(db, inv.Id, "John Doe", "john-doe");
        var ctrl = TestSupport.NewClientController(db, inv.Id);

        var result = await ctrl.UpdateGuest(guest.Id, new UpdateGuestRequest("José Muñoz", 2));

        var dto = TestSupport.Body<GuestDto>(result);
        Assert.Equal("jose-munoz", dto.Slug);
    }

    [Fact]
    public async Task Legacy_guest_without_slug_gets_one_on_edit()
    {
        using var db = TestSupport.NewDb();
        var inv = TestSupport.SeedInvitation(db);
        var guest = TestSupport.SeedGuest(db, inv.Id, "Old Guest", slug: "");
        var ctrl = TestSupport.NewClientController(db, inv.Id);

        // Even a no-op name edit backfills a slug for a legacy row.
        var result = await ctrl.UpdateGuest(guest.Id, new UpdateGuestRequest("Old Guest", 3));

        var dto = TestSupport.Body<GuestDto>(result);
        Assert.Equal("old-guest", dto.Slug);
    }

    [Fact]
    public async Task CreateGuest_assigns_a_name_based_slug()
    {
        using var db = TestSupport.NewDb();
        var inv = TestSupport.SeedInvitation(db);
        var ctrl = TestSupport.NewClientController(db, inv.Id);

        var result = await ctrl.CreateGuest(new CreateGuestRequest("Charbel Nahhas", 2));

        var dto = TestSupport.Body<GuestDto>(result);
        Assert.Equal("charbel-nahhas", dto.Slug);
    }
}
