using InvitationPlatform.Api.Dtos;
using InvitationPlatform.Api.Services;
using InvitationPlatform.Domain.Entities;
using InvitationPlatform.Domain.Enums;
using InvitationPlatform.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InvitationPlatform.Api.Controllers;

[ApiController]
[Route("api/public")]
[AllowAnonymous]
public class PublicController(AppDbContext db) : ControllerBase
{
    /// <summary>Loads a published invitation by slug or public token.</summary>
    [HttpGet("invitations/{key}")]
    public async Task<IActionResult> GetInvitation(string key)
    {
        var inv = await db.Invitations
            .Include(i => i.Sections).ThenInclude(s => s.Locations)
            .Include(i => i.Sections).ThenInclude(s => s.GiftAccounts)
            .FirstOrDefaultAsync(i => (i.Slug == key || i.PublicToken == key)
                                      && i.Status == InvitationStatus.Published);

        if (inv is null) return NotFound(new { error = "Invitation not found" });

        var data = InvitationDataMapper.ToData(inv);
        return Ok(new
        {
            id = inv.Id,
            slug = inv.Slug,
            title = inv.Title,
            eventDate = inv.EventDate,
            data
        });
    }

    /// <summary>Submits an RSVP from a guest (no auth).</summary>
    [HttpPost("invitations/{slug}/rsvp")]
    public async Task<IActionResult> SubmitRsvp(string slug, [FromBody] SubmitRsvpRequest req)
    {
        var inv = await db.Invitations
            .FirstOrDefaultAsync(i => i.Slug == slug && i.Status == InvitationStatus.Published);
        if (inv is null) return NotFound(new { error = "Invitation not found" });

        if (!Enum.TryParse<RsvpResponse>(req.Response, true, out var response))
            return BadRequest(new { error = "Response must be 'yes', 'no', or 'maybe'" });

        var rsvp = new Rsvp
        {
            InvitationId = inv.Id,
            Response = response,
            PartySize = req.PartySize,
            ContactName = req.ContactName,
            ContactEmail = req.ContactEmail,
            ContactPhone = req.ContactPhone,
            Message = req.Message,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = Request.Headers.UserAgent.ToString()
        };
        foreach (var (g, i) in (req.Guests ?? []).Select((g, i) => (g, i)))
        {
            rsvp.Guests.Add(new RsvpGuest
            {
                OrderIndex = i,
                FullName = g.FullName,
                AgeGroup = g.AgeGroup,
                MealPreference = g.MealPreference,
                DietaryRestrictions = g.DietaryRestrictions
            });
        }
        db.Rsvps.Add(rsvp);
        await db.SaveChangesAsync();
        return Ok(new { ok = true, rsvpId = rsvp.Id });
    }
}
