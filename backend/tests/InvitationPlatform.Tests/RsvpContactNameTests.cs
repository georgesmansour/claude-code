using InvitationPlatform.Api.Dtos;
using InvitationPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace InvitationPlatform.Tests;

/// <summary>
/// Bug #1 — a declined RSVP must always keep the guest's name so the dashboard never shows "-".
/// The name lives on Rsvp.ContactName (declines carry no per-attendee rows), and the server must
/// backfill it from the guest record even when the client sends nothing usable.
/// </summary>
public class RsvpContactNameTests
{
    private static SubmitRsvpRequest Decline(string? contactName, string? guestKey) => new(
        Response: "no", PartySize: 0,
        ContactName: contactName, ContactEmail: null, ContactPhone: null,
        Message: null, Guests: new List<RsvpGuestRequest>(), GuestToken: guestKey);

    [Fact]
    public async Task Decline_via_personal_link_keeps_guest_name_when_client_sends_name()
    {
        using var db = TestSupport.NewDb();
        var inv = TestSupport.SeedInvitation(db);
        var guest = TestSupport.SeedGuest(db, inv.Id, "Charbel Nahhas", "charbel-nahhas", token: "tok-1");
        var ctrl = TestSupport.NewPublicController(db);

        await ctrl.SubmitRsvp(inv.Slug, Decline("Charbel Nahhas", "charbel-nahhas"));

        var rsvp = await db.Rsvps.SingleAsync();
        Assert.Equal(RsvpResponse.No, rsvp.Response);
        Assert.Equal("Charbel Nahhas", rsvp.ContactName);
        Assert.Equal(guest.Id, rsvp.GuestId);
    }

    [Fact]
    public async Task Decline_backfills_name_from_guest_when_client_sends_null()
    {
        using var db = TestSupport.NewDb();
        var inv = TestSupport.SeedInvitation(db);
        TestSupport.SeedGuest(db, inv.Id, "Charbel Nahhas", "charbel-nahhas", token: "tok-1");
        var ctrl = TestSupport.NewPublicController(db);

        await ctrl.SubmitRsvp(inv.Slug, Decline(null, "charbel-nahhas"));

        var rsvp = await db.Rsvps.SingleAsync();
        Assert.Equal("Charbel Nahhas", rsvp.ContactName);
    }

    [Fact]
    public async Task Decline_backfills_name_from_guest_when_client_sends_blank_string()
    {
        // The old "req.ContactName ?? guest.Name" let "" through — this is the exact regression.
        using var db = TestSupport.NewDb();
        var inv = TestSupport.SeedInvitation(db);
        TestSupport.SeedGuest(db, inv.Id, "Charbel Nahhas", "charbel-nahhas", token: "tok-1");
        var ctrl = TestSupport.NewPublicController(db);

        await ctrl.SubmitRsvp(inv.Slug, Decline("   ", "charbel-nahhas"));

        var rsvp = await db.Rsvps.SingleAsync();
        Assert.Equal("Charbel Nahhas", rsvp.ContactName);
    }

    [Fact]
    public async Task Decline_via_legacy_token_still_resolves_the_guest_name()
    {
        using var db = TestSupport.NewDb();
        var inv = TestSupport.SeedInvitation(db);
        TestSupport.SeedGuest(db, inv.Id, "Rita Aoun", "rita-aoun", token: "legacy-token-xyz");
        var ctrl = TestSupport.NewPublicController(db);

        await ctrl.SubmitRsvp(inv.Slug, Decline(null, "legacy-token-xyz"));

        var rsvp = await db.Rsvps.SingleAsync();
        Assert.Equal("Rita Aoun", rsvp.ContactName);
    }

    [Fact]
    public async Task Decline_marks_guest_NotAccepted_with_zero_seats()
    {
        using var db = TestSupport.NewDb();
        var inv = TestSupport.SeedInvitation(db);
        var guest = TestSupport.SeedGuest(db, inv.Id, "Charbel Nahhas", "charbel-nahhas", token: "tok-1");
        var ctrl = TestSupport.NewPublicController(db);

        await ctrl.SubmitRsvp(inv.Slug, Decline("Charbel Nahhas", "charbel-nahhas"));

        var reloaded = await db.Guests.SingleAsync(g => g.Id == guest.Id);
        Assert.Equal(GuestRsvpStatus.NotAccepted, reloaded.Status);
        Assert.Equal(0, reloaded.SelectedAttendees);
        Assert.NotNull(reloaded.RespondedAt);
    }

    [Fact]
    public async Task Guest_cannot_rename_themselves_on_a_personal_link()
    {
        // Someone editing the request (or the DOM) must not be able to re-attribute the reply:
        // for a known guest the guest-list name always wins over whatever the browser sent.
        using var db = TestSupport.NewDb();
        var inv = TestSupport.SeedInvitation(db);
        TestSupport.SeedGuest(db, inv.Id, "John Doe", "john-doe", token: "tok-1");
        var ctrl = TestSupport.NewPublicController(db);

        await ctrl.SubmitRsvp(inv.Slug, new SubmitRsvpRequest(
            "yes", 1, "Somebody Else", null, null, null,
            new List<RsvpGuestRequest>(), "john-doe"));

        var rsvp = await db.Rsvps.SingleAsync();
        Assert.Equal("John Doe", rsvp.ContactName);
    }

    [Fact]
    public async Task Guest_cannot_rename_themselves_when_declining()
    {
        using var db = TestSupport.NewDb();
        var inv = TestSupport.SeedInvitation(db);
        TestSupport.SeedGuest(db, inv.Id, "John Doe", "john-doe", token: "tok-1");
        var ctrl = TestSupport.NewPublicController(db);

        await ctrl.SubmitRsvp(inv.Slug, Decline("Somebody Else", "john-doe"));

        var rsvp = await db.Rsvps.SingleAsync();
        Assert.Equal("John Doe", rsvp.ContactName);
    }

    [Fact]
    public async Task Anonymous_submission_may_still_supply_its_own_name()
    {
        // The lock only applies to personal links; an open invitation has no guest to trust.
        using var db = TestSupport.NewDb();
        var inv = TestSupport.SeedInvitation(db);
        var ctrl = TestSupport.NewPublicController(db);

        await ctrl.SubmitRsvp(inv.Slug, new SubmitRsvpRequest(
            "yes", 1, "Walk-in Guest", null, null, null,
            new List<RsvpGuestRequest>(), GuestToken: null));

        var rsvp = await db.Rsvps.SingleAsync();
        Assert.Equal("Walk-in Guest", rsvp.ContactName);
    }

    [Fact]
    public async Task Accept_then_decline_reuses_one_record_and_keeps_name()
    {
        // Re-submitting overwrites the same RSVP; the name must survive the accept→decline flip.
        using var db = TestSupport.NewDb();
        var inv = TestSupport.SeedInvitation(db);
        TestSupport.SeedGuest(db, inv.Id, "Charbel Nahhas", "charbel-nahhas", token: "tok-1");
        var ctrl = TestSupport.NewPublicController(db);

        await ctrl.SubmitRsvp(inv.Slug, new SubmitRsvpRequest(
            "yes", 2, "Charbel Nahhas", null, null, null,
            new List<RsvpGuestRequest> { new("Charbel Nahhas", null, null, null), new("Rita", null, null, null) },
            "charbel-nahhas"));
        await ctrl.SubmitRsvp(inv.Slug, Decline(null, "charbel-nahhas"));

        var rsvp = await db.Rsvps.SingleAsync();
        Assert.Equal(RsvpResponse.No, rsvp.Response);
        Assert.Equal(0, rsvp.PartySize);
        Assert.Equal("Charbel Nahhas", rsvp.ContactName);
        Assert.Empty(rsvp.Guests);   // per-attendee rows cleared on decline
    }
}
