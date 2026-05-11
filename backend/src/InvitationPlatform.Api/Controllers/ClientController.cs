using System.Security.Claims;
using InvitationPlatform.Api.Dtos;
using InvitationPlatform.Domain.Enums;
using InvitationPlatform.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InvitationPlatform.Api.Controllers;

[ApiController]
[Route("api/client")]
[Authorize(Roles = "Client")]
public class ClientController(AppDbContext db) : ControllerBase
{
    private Guid CurrentInvitationId =>
        Guid.Parse(User.FindFirst("invitation_id")!.Value);

    [HttpGet("dashboard/summary")]
    public async Task<IActionResult> Summary()
    {
        var inv = await db.Invitations
            .Where(i => i.Id == CurrentInvitationId)
            .Select(i => new { i.Id, i.Slug, i.Title, i.EventDate, i.MaxAttendees })
            .FirstOrDefaultAsync();
        if (inv is null) return NotFound();

        var rsvps = await db.Rsvps.Where(r => r.InvitationId == CurrentInvitationId).ToListAsync();
        var attending = rsvps.Count(r => r.Response == RsvpResponse.Yes);
        var declined  = rsvps.Count(r => r.Response == RsvpResponse.No);
        var maybe     = rsvps.Count(r => r.Response == RsvpResponse.Maybe);
        var seats     = rsvps.Where(r => r.Response == RsvpResponse.Yes).Sum(r => r.PartySize);
        var rate      = rsvps.Count == 0 ? 0 : Math.Round(attending * 100.0 / rsvps.Count, 1);

        return Ok(new DashboardSummary(
            inv.Id, inv.Slug, inv.Title, inv.EventDate, inv.MaxAttendees,
            rsvps.Count, attending, declined, maybe, seats, rate));
    }

    [HttpGet("dashboard/attendees")]
    public async Task<IActionResult> Attendees([FromQuery] string? response)
    {
        var q = db.Rsvps.Include(r => r.Guests).Where(r => r.InvitationId == CurrentInvitationId);

        if (!string.IsNullOrEmpty(response) &&
            Enum.TryParse<RsvpResponse>(response, true, out var r))
            q = q.Where(x => x.Response == r);

        var rows = await q.OrderByDescending(x => x.CreatedAt).ToListAsync();
        return Ok(rows.Select(x => new RsvpDto(
            x.Id, x.Response.ToString(), x.PartySize,
            x.ContactName, x.ContactEmail, x.ContactPhone, x.Message, x.CreatedAt,
            x.Guests.OrderBy(g => g.OrderIndex).Select(g =>
                new RsvpGuestDto(g.FullName, g.AgeGroup, g.MealPreference, g.DietaryRestrictions)).ToList())));
    }
}
