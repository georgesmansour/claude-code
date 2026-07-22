using System.Security.Claims;
using System.Security.Cryptography;
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
        var seats     = rsvps.Where(r => r.Response == RsvpResponse.Yes).Sum(r => r.PartySize);
        var rate      = rsvps.Count == 0 ? 0 : Math.Round(attending * 100.0 / rsvps.Count, 1);

        return Ok(new DashboardSummary(
            inv.Id, inv.Slug, inv.Title, inv.EventDate, inv.MaxAttendees,
            rsvps.Count, attending, declined, seats, rate));
    }

    [HttpGet("dashboard/attendees")]
    public async Task<IActionResult> Attendees([FromQuery] string? response)
    {
        var q = db.Rsvps.Include(r => r.Guests).Where(r => r.InvitationId == CurrentInvitationId);

        if (!string.IsNullOrEmpty(response))
        {
            if (!Enum.TryParse<RsvpResponse>(response, true, out var r))
                return BadRequest(new { error = "Response must be 'yes' or 'no'" });
            q = q.Where(x => x.Response == r);
        }

        var rows = await q.OrderByDescending(x => x.CreatedAt).ToListAsync();
        return Ok(rows.Select(x => new RsvpDto(
            x.Id, x.Response.ToString(), x.PartySize,
            x.ContactName, x.ContactEmail, x.ContactPhone, x.Message, x.CreatedAt,
            x.Guests.OrderBy(g => g.OrderIndex).Select(g =>
                new RsvpGuestDto(g.FullName, g.AgeGroup, g.MealPreference, g.DietaryRestrictions)).ToList())));
    }

    // ── GUEST LIST MANAGEMENT ────────────────────────────────

    private const int MaxAttendeesLimit = 100;

    private static string NewGuestToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(12))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');

    private static GuestDto ToGuestDto(Guest g) => new(
        g.Id, g.Name, g.MaxAttendees, g.SelectedAttendees,
        g.Status.ToString(), g.Token, g.Slug, g.RespondedAt, g.UpdatedAt);

    /// <summary>
    /// Builds a globally-unique, name-based slug. Appends "-2", "-3", … on collision.
    /// <paramref name="reserved"/> lets bulk imports reserve slugs before they hit the DB.
    /// </summary>
    private async Task<string> UniqueSlugAsync(string name, Guid? excludeId = null, HashSet<string>? reserved = null)
    {
        var baseSlug = SlugHelper.Slugify(name);
        var candidate = baseSlug;
        var n = 2;
        while ((reserved?.Contains(candidate) ?? false)
               || await db.Guests.AnyAsync(g => g.Slug == candidate && (excludeId == null || g.Id != excludeId)))
        {
            candidate = $"{baseSlug}-{n++}";
        }
        reserved?.Add(candidate);
        return candidate;
    }

    [HttpGet("guests")]
    public async Task<IActionResult> ListGuests()
    {
        var guests = await db.Guests
            .Where(g => g.InvitationId == CurrentInvitationId)
            .OrderBy(g => g.Name)
            .ToListAsync();
        return Ok(guests.Select(ToGuestDto));
    }

    [HttpPost("guests")]
    public async Task<IActionResult> CreateGuest([FromBody] CreateGuestRequest req)
    {
        var name = (req.Name ?? "").Trim();
        if (name.Length == 0) return BadRequest(new { error = "Name is required" });
        if (name.Length > 256) return BadRequest(new { error = "Name is longer than 256 characters" });
        if (req.MaxAttendees < 1 || req.MaxAttendees > MaxAttendeesLimit)
            return BadRequest(new { error = $"Max attendees must be between 1 and {MaxAttendeesLimit}" });

        var exists = await db.Guests.AnyAsync(g =>
            g.InvitationId == CurrentInvitationId && g.Name.ToLower() == name.ToLower());
        if (exists) return Conflict(new { error = "A guest with this name already exists" });

        var guest = new Guest
        {
            InvitationId = CurrentInvitationId,
            Name = name,
            MaxAttendees = req.MaxAttendees,
            Token = NewGuestToken(),
            Slug = await UniqueSlugAsync(name)
        };
        db.Guests.Add(guest);
        await db.SaveChangesAsync();
        return Ok(ToGuestDto(guest));
    }

    /// <summary>
    /// Bulk upsert from an Excel/CSV import (parsed client-side).
    /// Matches existing guests case-insensitively by name; matches keep their
    /// invitation token so previously shared links stay valid.
    /// </summary>
    [HttpPost("guests/import")]
    public async Task<IActionResult> ImportGuests([FromBody] ImportGuestsRequest req)
    {
        if (req.Rows is null || req.Rows.Count == 0)
            return BadRequest(new { error = "No rows to import" });
        if (req.Rows.Count > 10000)
            return BadRequest(new { error = "Import is limited to 10,000 rows per file" });

        var existing = await db.Guests
            .Where(g => g.InvitationId == CurrentInvitationId)
            .ToListAsync();
        var byName = existing
            .GroupBy(g => g.Name.Trim().ToLowerInvariant())
            .ToDictionary(x => x.Key, x => x.First());

        // Reserve every slug already in use across ALL invitations so newly-created
        // guests get globally-unique, name-based slugs without one DB round-trip per row.
        var reserved = (await db.Guests.Select(g => g.Slug).ToListAsync())
            .Where(s => !string.IsNullOrEmpty(s))
            .ToHashSet();

        var created = 0;
        var updated = 0;
        var failed = new List<ImportRowError>();
        var seenInFile = new HashSet<string>();

        foreach (var row in req.Rows)
        {
            var name = (row.Name ?? "").Trim();
            if (name.Length == 0)
            {
                failed.Add(new ImportRowError(row.Row, "Name is missing"));
                continue;
            }
            if (name.Length > 256)
            {
                failed.Add(new ImportRowError(row.Row, "Name is longer than 256 characters"));
                continue;
            }
            if (row.MaxAttendees is null)
            {
                failed.Add(new ImportRowError(row.Row, "Maximum allowed attendees is missing or not a number"));
                continue;
            }
            if (row.MaxAttendees < 1 || row.MaxAttendees > MaxAttendeesLimit)
            {
                failed.Add(new ImportRowError(row.Row, $"Maximum allowed attendees must be between 1 and {MaxAttendeesLimit}"));
                continue;
            }

            var key = name.ToLowerInvariant();
            if (!seenInFile.Add(key))
            {
                failed.Add(new ImportRowError(row.Row, $"Duplicate name in file: \"{name}\""));
                continue;
            }

            if (byName.TryGetValue(key, out var guest))
            {
                // Update in place — token and slug (and therefore the shared link) are preserved.
                guest.Name = name;
                guest.MaxAttendees = row.MaxAttendees.Value;
                if (string.IsNullOrEmpty(guest.Slug))          // legacy guest without a slug yet
                    guest.Slug = await UniqueSlugAsync(name, guest.Id, reserved);
                guest.UpdatedAt = DateTime.UtcNow;
                updated++;
            }
            else
            {
                db.Guests.Add(new Guest
                {
                    InvitationId = CurrentInvitationId,
                    Name = name,
                    MaxAttendees = row.MaxAttendees.Value,
                    Token = NewGuestToken(),
                    Slug = await UniqueSlugAsync(name, null, reserved)
                });
                created++;
            }
        }

        await db.SaveChangesAsync();
        return Ok(new ImportGuestsResult(created, updated, failed));
    }

    [HttpPut("guests/{id:guid}")]
    public async Task<IActionResult> UpdateGuest(Guid id, [FromBody] UpdateGuestRequest req)
    {
        var guest = await db.Guests
            .FirstOrDefaultAsync(g => g.Id == id && g.InvitationId == CurrentInvitationId);
        if (guest is null) return NotFound(new { error = "Guest not found" });

        var name = (req.Name ?? "").Trim();
        if (name.Length == 0) return BadRequest(new { error = "Name is required" });
        if (name.Length > 256) return BadRequest(new { error = "Name is longer than 256 characters" });
        if (req.MaxAttendees < 1 || req.MaxAttendees > MaxAttendeesLimit)
            return BadRequest(new { error = $"Max attendees must be between 1 and {MaxAttendeesLimit}" });

        var duplicate = await db.Guests.AnyAsync(g =>
            g.InvitationId == CurrentInvitationId && g.Id != id && g.Name.ToLower() == name.ToLower());
        if (duplicate) return Conflict(new { error = "Another guest already has this name" });

        guest.Name = name;
        guest.MaxAttendees = req.MaxAttendees;
        // The slug (and shared link) stays stable across renames; only assign one if missing (legacy row).
        if (string.IsNullOrEmpty(guest.Slug))
            guest.Slug = await UniqueSlugAsync(name, guest.Id);
        guest.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok(ToGuestDto(guest));
    }

    [HttpDelete("guests/{id:guid}")]
    public async Task<IActionResult> DeleteGuest(Guid id)
    {
        var guest = await db.Guests
            .FirstOrDefaultAsync(g => g.Id == id && g.InvitationId == CurrentInvitationId);
        if (guest is null) return NotFound(new { error = "Guest not found" });
        db.Guests.Remove(guest);
        await db.SaveChangesAsync();
        return Ok(new { ok = true });
    }

    /// <summary>Issues a fresh, name-based link; the previously shared one stops working.</summary>
    [HttpPost("guests/{id:guid}/regenerate-link")]
    public async Task<IActionResult> RegenerateGuestLink(Guid id)
    {
        var guest = await db.Guests
            .FirstOrDefaultAsync(g => g.Id == id && g.InvitationId == CurrentInvitationId);
        if (guest is null) return NotFound(new { error = "Guest not found" });
        // A random suffix guarantees the new URL differs from the old one, invalidating the shared link.
        guest.Slug = await UniqueSlugAsync($"{guest.Name} {SlugHelper.RandomSuffix()}", guest.Id);
        guest.Token = NewGuestToken();
        guest.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok(ToGuestDto(guest));
    }
}
