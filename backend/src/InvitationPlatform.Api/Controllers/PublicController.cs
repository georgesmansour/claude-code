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
        // Slug lookups require Published; the secret PublicToken also resolves
        // drafts so the admin/couple can preview before publishing.
        var inv = await db.Invitations
            .Include(i => i.Template)
            .Include(i => i.Sections).ThenInclude(s => s.Locations)
            .Include(i => i.Sections).ThenInclude(s => s.GiftAccounts)
            .FirstOrDefaultAsync(i =>
                (i.Slug == key && i.Status == InvitationStatus.Published)
                || i.PublicToken == key);

        if (inv is null) return NotFound(new { error = "Invitation not found" });

        // Derive a URL-safe key from the template name, e.g. "Classic Wedding" → "classic-wedding"
        var templateKey = inv.Template is not null
            ? inv.Template.Name.ToLowerInvariant().Replace(' ', '-')
            : "classic-wedding";

        var data = InvitationDataMapper.ToData(inv);
        return Ok(new
        {
            id = inv.Id,
            slug = inv.Slug,
            title = inv.Title,
            eventDate = inv.EventDate,
            templateKey,
            data
        });
    }

    /// <summary>
    /// Resolves a personal guest link to its invitation + guest info.
    /// <paramref name="key"/> is the name-based slug (e.g. "charbel-nahhas"); the older
    /// random token is still accepted so links shared before this change keep working.
    /// Guests never type their name.
    /// </summary>
    [HttpGet("guest/{key}")]
    public async Task<IActionResult> GetByGuestKey(string key)
    {
        var guest = await db.Guests
            .Include(g => g.Invitation).ThenInclude(i => i.Template)
            .Include(g => g.Invitation).ThenInclude(i => i.Sections).ThenInclude(s => s.Locations)
            .Include(g => g.Invitation).ThenInclude(i => i.Sections).ThenInclude(s => s.GiftAccounts)
            .FirstOrDefaultAsync(g => (g.Slug == key || g.Token == key)
                                      && g.Invitation.Status == InvitationStatus.Published);
        if (guest is null) return NotFound(new { error = "Invitation not found" });

        var inv = guest.Invitation;
        var templateKey = inv.Template is not null
            ? inv.Template.Name.ToLowerInvariant().Replace(' ', '-')
            : "classic-wedding";

        return Ok(new
        {
            id = inv.Id,
            slug = inv.Slug,
            title = inv.Title,
            eventDate = inv.EventDate,
            templateKey,
            data = InvitationDataMapper.ToData(inv),
            guest = new
            {
                name = guest.Name,
                slug = guest.Slug,
                maxAttendees = guest.MaxAttendees,
                selectedAttendees = guest.SelectedAttendees,
                status = guest.Status.ToString()
            }
        });
    }

    /// <summary>Submits an RSVP from a guest (no auth).</summary>
    [HttpPost("invitations/{slug}/rsvp")]
    public async Task<IActionResult> SubmitRsvp(string slug, [FromBody] SubmitRsvpRequest req)
    {
        var inv = await db.Invitations
            .FirstOrDefaultAsync(i => i.Slug == slug && i.Status == InvitationStatus.Published);
        if (inv is null) return NotFound(new { error = "Invitation not found" });

        // Accept only the literal "yes"/"no" — Enum.TryParse would otherwise let numeric
        // strings like "5" through and silently treat them as a decline.
        RsvpResponse response;
        switch ((req.Response ?? "").Trim().ToLowerInvariant())
        {
            case "yes": response = RsvpResponse.Yes; break;
            case "no":  response = RsvpResponse.No;  break;
            default: return BadRequest(new { error = "Response must be 'yes' or 'no'" });
        }

        // An acceptance must bring at least one attendee (guards against 0 / negative payloads).
        if (response == RsvpResponse.Yes && req.PartySize < 1)
            return BadRequest(new { error = "Please select at least 1 attendee" });

        // Personal link: the guest's own allowance takes precedence over the
        // invitation-wide cap, and the guest record is updated with the reply.
        // The key may be the name-based slug or the legacy random token.
        Guest? guest = null;
        if (!string.IsNullOrEmpty(req.GuestToken))
        {
            guest = await db.Guests.FirstOrDefaultAsync(g =>
                (g.Slug == req.GuestToken || g.Token == req.GuestToken) && g.InvitationId == inv.Id);
            if (guest is null)
                return BadRequest(new { error = "This invitation link is no longer valid" });
            if (response == RsvpResponse.Yes && req.PartySize > guest.MaxAttendees)
                return BadRequest(new { error = $"Party size cannot exceed {guest.MaxAttendees}" });
        }
        else if (response == RsvpResponse.Yes && inv.MaxAttendees > 0 && req.PartySize > inv.MaxAttendees)
        {
            return BadRequest(new { error = $"Party size cannot exceed {inv.MaxAttendees}" });
        }

        // A declining guest brings zero attendees, regardless of what was submitted.
        var partySize = response == RsvpResponse.Yes ? req.PartySize : 0;

        // One guest = one RSVP record. For a personal link we reuse the guest's existing
        // record (created via any earlier link/token, or a pre-slug anonymous row that
        // matched their name) so re-submissions OVERWRITE instead of piling up.
        Rsvp? rsvp = null;
        if (guest is not null)
        {
            var priorForGuest = await db.Rsvps
                .Include(r => r.Guests)
                .Where(r => r.InvitationId == inv.Id
                            && (r.GuestId == guest.Id
                                || (r.GuestId == null && r.ContactName == guest.Name)))
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            if (priorForGuest.Count > 0)
            {
                rsvp = priorForGuest[0];
                // Drop any stray duplicates from before this fix so the count is exactly one.
                if (priorForGuest.Count > 1)
                    db.Rsvps.RemoveRange(priorForGuest.Skip(1));
            }
        }

        if (rsvp is null)
        {
            rsvp = new Rsvp
            {
                InvitationId = inv.Id,
                GuestId = guest?.Id,
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                UserAgent = Request.Headers.UserAgent.ToString()
            };
            db.Rsvps.Add(rsvp);
        }

        rsvp.GuestId = guest?.Id;
        rsvp.Response = response;
        rsvp.PartySize = partySize;
        rsvp.ContactName = req.ContactName ?? guest?.Name;
        rsvp.ContactEmail = req.ContactEmail;
        rsvp.ContactPhone = req.ContactPhone;
        rsvp.Message = req.Message;

        // Replace the per-attendee name rows with the latest submission.
        rsvp.Guests.Clear();
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

        if (guest is not null)
        {
            guest.Status = response == RsvpResponse.Yes ? GuestRsvpStatus.Accepted : GuestRsvpStatus.NotAccepted;
            guest.SelectedAttendees = partySize;
            guest.RespondedAt = DateTime.UtcNow;
            guest.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();
        return Ok(new { ok = true, rsvpId = rsvp.Id });
    }
}
