using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
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
[Route("api/admin")]
[Authorize(Roles = "Admin")]
public class AdminController(AppDbContext db) : ControllerBase
{
    private Guid CurrentAdminId =>
        Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    // ── TEMPLATES ────────────────────────────────────────────
    [HttpGet("templates")]
    public async Task<IActionResult> ListTemplates()
    {
        var rows = await db.Templates
            .Where(t => t.IsActive)
            .OrderByDescending(t => t.IsBuiltin).ThenBy(t => t.Name)
            .ToListAsync();
        return Ok(rows.Select(t => new TemplateDto(
            t.Id, t.Name, t.Description, t.IsBuiltin, t.IsActive,
            JsonSerializer.Deserialize<InvitationData>(t.Data, Json) ?? new())));
    }

    [HttpPost("templates")]
    public async Task<IActionResult> CreateTemplate([FromBody] CreateTemplateRequest req)
    {
        var t = new Template
        {
            CreatedBy = CurrentAdminId,
            Name = req.Name,
            Description = req.Description,
            IsBuiltin = false,
            IsActive = true,
            Data = JsonSerializer.Serialize(req.Data, Json)
        };
        db.Templates.Add(t);
        await db.SaveChangesAsync();
        return Ok(new { id = t.Id });
    }

    [HttpPut("templates/{id}")]
    public async Task<IActionResult> UpdateTemplate(Guid id, [FromBody] UpdateTemplateRequest req)
    {
        var t = await db.Templates.FindAsync(id);
        if (t is null) return NotFound();
        if (t.IsBuiltin) return BadRequest(new { error = "Cannot modify built-in templates" });
        t.Name = req.Name;
        t.Description = req.Description;
        t.IsActive = req.IsActive;
        t.Data = JsonSerializer.Serialize(req.Data, Json);
        await db.SaveChangesAsync();
        return Ok();
    }

    [HttpDelete("templates/{id}")]
    public async Task<IActionResult> DeleteTemplate(Guid id)
    {
        var t = await db.Templates.FindAsync(id);
        if (t is null) return NotFound();
        if (t.IsBuiltin) return BadRequest(new { error = "Cannot delete built-in templates" });
        db.Templates.Remove(t);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ── INVITATIONS ──────────────────────────────────────────
    [HttpGet("invitations")]
    public async Task<IActionResult> ListInvitations()
    {
        var rows = await db.Invitations
            .OrderByDescending(i => i.UpdatedAt)
            .Select(i => new InvitationListItem(
                i.Id, i.Slug, i.Title, i.Status.ToString(),
                i.EventDate,
                i.Rsvps.Count,
                i.UpdatedAt))
            .ToListAsync();
        return Ok(rows);
    }

    [HttpGet("invitations/{id}")]
    public async Task<IActionResult> GetInvitation(Guid id)
    {
        var inv = await db.Invitations
            .Include(i => i.Sections).ThenInclude(s => s.Locations)
            .Include(i => i.Sections).ThenInclude(s => s.GiftAccounts)
            .FirstOrDefaultAsync(i => i.Id == id);
        if (inv is null) return NotFound();

        return Ok(new InvitationFull(
            inv.Id, inv.Slug, inv.Title, inv.Status.ToString(),
            inv.EventType, inv.EventDate, inv.MaxAttendees, inv.TemplateId,
            inv.UpdatedAt, InvitationDataMapper.ToData(inv)));
    }

    [HttpPost("invitations")]
    public async Task<IActionResult> CreateInvitation([FromBody] CreateInvitationRequest req)
    {
        if (await db.Invitations.AnyAsync(i => i.Slug == req.Slug))
            return Conflict(new { error = "Slug already exists" });

        var inv = new Invitation
        {
            CreatedBy = CurrentAdminId,
            TemplateId = req.TemplateId,
            Slug = req.Slug,
            Title = req.Title,
            EventType = req.EventType,
            EventDate = req.EventDate.HasValue
                ? DateTime.SpecifyKind(req.EventDate.Value.Date, DateTimeKind.Utc)
                : null,
            Status = InvitationStatus.Draft,
            PublicToken = RandomHex(24)
        };
        InvitationDataMapper.ApplyData(inv, req.Data);
        db.Invitations.Add(inv);
        await db.SaveChangesAsync();
        return Ok(new { id = inv.Id, slug = inv.Slug });
    }

    [HttpPut("invitations/{id}")]
    public async Task<IActionResult> UpdateInvitation(Guid id, [FromBody] UpdateInvitationRequest req)
    {
        var inv = await db.Invitations
            .Include(i => i.Sections).ThenInclude(s => s.Locations)
            .Include(i => i.Sections).ThenInclude(s => s.GiftAccounts)
            .FirstOrDefaultAsync(i => i.Id == id);
        if (inv is null) return NotFound();

        if (inv.Slug != req.Slug && await db.Invitations.AnyAsync(i => i.Slug == req.Slug))
            return Conflict(new { error = "Slug already taken" });

        inv.Title = req.Title;
        inv.Slug = req.Slug;
        inv.EventType = req.EventType;
        inv.EventDate = req.EventDate.HasValue
            ? DateTime.SpecifyKind(req.EventDate.Value.Date, DateTimeKind.Utc)
            : null;
        inv.MaxAttendees = req.MaxAttendees;
        inv.UpdatedAt = DateTime.UtcNow;
        InvitationDataMapper.ApplyData(inv, req.Data);
        await db.SaveChangesAsync();
        return Ok();
    }

    [HttpPost("invitations/{id}/publish")]
    public async Task<IActionResult> Publish(Guid id)
    {
        var inv = await db.Invitations.FindAsync(id);
        if (inv is null) return NotFound();
        inv.Status = InvitationStatus.Published;
        inv.PublishedAt = DateTime.UtcNow;
        inv.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok();
    }

    [HttpPost("invitations/{id}/unpublish")]
    public async Task<IActionResult> Unpublish(Guid id)
    {
        var inv = await db.Invitations.FindAsync(id);
        if (inv is null) return NotFound();
        inv.Status = InvitationStatus.Draft;
        inv.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok();
    }

    [HttpDelete("invitations/{id}")]
    public async Task<IActionResult> DeleteInvitation(Guid id)
    {
        var inv = await db.Invitations.FindAsync(id);
        if (inv is null) return NotFound();
        db.Invitations.Remove(inv);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ── CLIENTS (couple credentials) ─────────────────────────
    [HttpGet("invitations/{id}/client")]
    public async Task<IActionResult> GetClient(Guid id)
    {
        var c = await db.ClientAccounts.FirstOrDefaultAsync(c => c.InvitationId == id);
        if (c is null) return NotFound();
        return Ok(new ClientAccountDto(c.Id, c.InvitationId, c.Email, c.FullName,
            c.Phone, c.IsActive, c.MustChangePassword, c.LastLoginAt, c.CreatedAt));
    }

    [HttpPost("clients")]
    public async Task<IActionResult> CreateClient([FromBody] CreateClientRequest req)
    {
        if (await db.ClientAccounts.AnyAsync(c => c.InvitationId == req.InvitationId))
            return Conflict(new { error = "This invitation already has a client account" });
        if (await db.ClientAccounts.AnyAsync(c => c.Email == req.Email))
            return Conflict(new { error = "Email already in use" });
        if (req.Password.Length < 6)
            return BadRequest(new { error = "Password must be at least 6 characters" });

        var client = new ClientAccount
        {
            InvitationId = req.InvitationId,
            CreatedBy = CurrentAdminId,
            Email = req.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
            FullName = req.FullName,
            Phone = req.Phone,
            MustChangePassword = true
        };
        db.ClientAccounts.Add(client);
        await db.SaveChangesAsync();
        return Ok(new { id = client.Id });
    }

    // ── RSVPs ────────────────────────────────────────────────
    [HttpGet("invitations/{id}/rsvps")]
    public async Task<IActionResult> GetRsvps(Guid id)
    {
        var rsvps = await db.Rsvps
            .Include(r => r.Guests)
            .Where(r => r.InvitationId == id)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        return Ok(rsvps.Select(r => new RsvpDto(
            r.Id, r.Response.ToString(), r.PartySize,
            r.ContactName, r.ContactEmail, r.ContactPhone, r.Message, r.CreatedAt,
            r.Guests.OrderBy(g => g.OrderIndex).Select(g =>
                new RsvpGuestDto(g.FullName, g.AgeGroup, g.MealPreference, g.DietaryRestrictions)).ToList())));
    }

    private static string RandomHex(int bytes)
    {
        var buf = new byte[bytes];
        RandomNumberGenerator.Fill(buf);
        return Convert.ToHexString(buf).ToLowerInvariant();
    }
}
