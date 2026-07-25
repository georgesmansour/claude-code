using InvitationPlatform.Api.Dtos;
using InvitationPlatform.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InvitationPlatform.Tests;

/// <summary>
/// Regression guards for existing RSVP behavior that the bug-fixes must not break:
/// acceptance flow, party-size caps, response validation, and anonymous submissions.
/// </summary>
public class RsvpRegressionTests
{
    [Fact]
    public async Task Accept_via_personal_link_stores_attendee_rows_and_seats()
    {
        using var db = TestSupport.NewDb();
        var inv = TestSupport.SeedInvitation(db);
        var guest = TestSupport.SeedGuest(db, inv.Id, "Charbel Nahhas", "charbel-nahhas", maxAttendees: 4, token: "tok-1");
        var ctrl = TestSupport.NewPublicController(db);

        await ctrl.SubmitRsvp(inv.Slug, new SubmitRsvpRequest(
            "yes", 2, "Charbel Nahhas", null, null, null,
            new List<RsvpGuestRequest> { new("Charbel Nahhas", null, null, null), new("Rita Nahhas", null, null, null) },
            "charbel-nahhas"));

        var rsvp = await db.Rsvps.Include(r => r.Guests).SingleAsync();
        Assert.Equal(RsvpResponse.Yes, rsvp.Response);
        Assert.Equal(2, rsvp.PartySize);
        Assert.Equal(2, rsvp.Guests.Count);

        var reloaded = await db.Guests.SingleAsync(g => g.Id == guest.Id);
        Assert.Equal(GuestRsvpStatus.Accepted, reloaded.Status);
        Assert.Equal(2, reloaded.SelectedAttendees);
    }

    [Fact]
    public async Task Accept_party_size_above_guest_allowance_is_rejected()
    {
        using var db = TestSupport.NewDb();
        var inv = TestSupport.SeedInvitation(db);
        TestSupport.SeedGuest(db, inv.Id, "Charbel Nahhas", "charbel-nahhas", maxAttendees: 2, token: "tok-1");
        var ctrl = TestSupport.NewPublicController(db);

        var result = await ctrl.SubmitRsvp(inv.Slug, new SubmitRsvpRequest(
            "yes", 5, "Charbel Nahhas", null, null, null,
            new List<RsvpGuestRequest>(), "charbel-nahhas"));

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(0, await db.Rsvps.CountAsync());
    }

    [Fact]
    public async Task Accept_party_size_above_invitation_cap_is_rejected_for_anonymous()
    {
        using var db = TestSupport.NewDb();
        var inv = TestSupport.SeedInvitation(db, maxAttendees: 3);
        var ctrl = TestSupport.NewPublicController(db);

        var result = await ctrl.SubmitRsvp(inv.Slug, new SubmitRsvpRequest(
            "yes", 4, "Walk-in Guest", null, null, null,
            new List<RsvpGuestRequest>(), GuestToken: null));

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Theory]
    [InlineData("maybe")]
    [InlineData("5")]
    [InlineData("")]
    public async Task Invalid_response_value_is_rejected(string response)
    {
        using var db = TestSupport.NewDb();
        var inv = TestSupport.SeedInvitation(db);
        var ctrl = TestSupport.NewPublicController(db);

        var result = await ctrl.SubmitRsvp(inv.Slug, new SubmitRsvpRequest(
            response, 1, "Someone", null, null, null,
            new List<RsvpGuestRequest>(), GuestToken: null));

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Anonymous_decline_is_accepted_and_stored()
    {
        using var db = TestSupport.NewDb();
        var inv = TestSupport.SeedInvitation(db);
        var ctrl = TestSupport.NewPublicController(db);

        var result = await ctrl.SubmitRsvp(inv.Slug, new SubmitRsvpRequest(
            "no", 0, "Walk-in Guest", null, null, null,
            new List<RsvpGuestRequest>(), GuestToken: null));

        Assert.IsType<OkObjectResult>(result);
        var rsvp = await db.Rsvps.SingleAsync();
        Assert.Equal(RsvpResponse.No, rsvp.Response);
        Assert.Equal("Walk-in Guest", rsvp.ContactName);
        Assert.Null(rsvp.GuestId);
    }

    [Fact]
    public async Task Unknown_invitation_slug_returns_not_found()
    {
        using var db = TestSupport.NewDb();
        TestSupport.SeedInvitation(db);
        var ctrl = TestSupport.NewPublicController(db);

        var result = await ctrl.SubmitRsvp("does-not-exist", new SubmitRsvpRequest(
            "yes", 1, "X", null, null, null, new List<RsvpGuestRequest>(), null));

        Assert.IsType<NotFoundObjectResult>(result);
    }
}
