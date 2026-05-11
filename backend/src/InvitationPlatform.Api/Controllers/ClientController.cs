using System.Security.Claims;
using InvitationPlatform.Api.Dtos;
using InvitationPlatform.Api.Services;
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

    // ── INVITATION SELF-EDIT ─────────────────────────────────

    [HttpGet("invitation")]
    public async Task<IActionResult> GetInvitation()
    {
        var inv = await db.Invitations
            .Include(i => i.Sections).ThenInclude(s => s.Locations)
            .Include(i => i.Sections).ThenInclude(s => s.GiftAccounts)
            .FirstOrDefaultAsync(i => i.Id == CurrentInvitationId);
        if (inv is null) return NotFound();
        return Ok(new InvitationFull(
            inv.Id, inv.Slug, inv.Title, inv.Status.ToString(),
            inv.EventType, inv.EventDate, inv.MaxAttendees, inv.TemplateId,
            inv.UpdatedAt, InvitationDataMapper.ToData(inv)));
    }

    [HttpPut("invitation")]
    public async Task<IActionResult> UpdateInvitation([FromBody] ClientUpdateInvitationRequest req)
    {
        var inv = await db.Invitations
            .Include(i => i.Sections).ThenInclude(s => s.Locations)
            .Include(i => i.Sections).ThenInclude(s => s.GiftAccounts)
            .FirstOrDefaultAsync(i => i.Id == CurrentInvitationId);
        if (inv is null) return NotFound();

        // Preserve admin-managed fields: images, section enabled/disabled, custom sections
        var current = InvitationDataMapper.ToData(inv);
        var d = req.Data;

        if (d.Cover != null)
        {
            d.Cover.Image   = current.Cover?.Image;
            d.Cover.Enabled = current.Cover?.Enabled ?? true;
        }
        if (d.Countdown != null)
        {
            d.Countdown.Image   = current.Countdown?.Image;
            d.Countdown.Enabled = current.Countdown?.Enabled ?? true;
        }
        if (d.Locations != null)
        {
            d.Locations.Image   = current.Locations?.Image;
            d.Locations.Enabled = current.Locations?.Enabled ?? true;
        }
        if (d.Gifts != null)
        {
            d.Gifts.Image   = current.Gifts?.Image;
            d.Gifts.Enabled = current.Gifts?.Enabled ?? true;
        }
        if (d.Rsvp != null)
        {
            d.Rsvp.Image   = current.Rsvp?.Image;
            d.Rsvp.Enabled = current.Rsvp?.Enabled ?? true;
        }

        d.CustomSections = current.CustomSections;

        inv.Title = req.Title;
        inv.UpdatedAt = DateTime.UtcNow;
        InvitationDataMapper.ApplyData(inv, d);
        await db.SaveChangesAsync();
        return Ok();
    }

    // ── DASHBOARD ────────────────────────────────────────────

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
