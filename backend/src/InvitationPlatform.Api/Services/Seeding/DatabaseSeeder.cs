using InvitationPlatform.Api.Auth;
using InvitationPlatform.Domain.Entities;
using InvitationPlatform.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace InvitationPlatform.Api.Services.Seeding;

/// <summary>
/// Idempotent startup seeding, extracted from Program.cs. Runs once per boot and only writes what
/// is actually missing — the Super Admin (from configuration), the built-in templates (a single
/// existence query, not one per template), and legacy guest-slug backfill.
/// </summary>
public class DatabaseSeeder(
    AppDbContext db,
    IOptions<SuperAdminSettings> superAdminOptions,
    ILogger<DatabaseSeeder> log)
{
    private readonly SuperAdminSettings _superAdmin = superAdminOptions.Value;

    public async Task SeedAsync(CancellationToken ct = default)
    {
        await EnsureSuperAdminAsync(ct);
        await RetireRemovedTemplatesAsync(ct);
        await SeedTemplatesAsync(ct);
        await BackfillGuestSlugsAsync(ct);
    }

    /// <summary>Creates the Super Admin only if it does not already exist (credentials from config).</summary>
    private async Task EnsureSuperAdminAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_superAdmin.Email))
        {
            log.LogWarning("SuperAdmin:Email is not configured — skipping Super Admin seeding.");
            return;
        }

        var existing = await db.AdminAccounts.FirstOrDefaultAsync(a => a.Email == _superAdmin.Email, ct);
        if (existing is null)
        {
            if (string.IsNullOrWhiteSpace(_superAdmin.Password))
            {
                log.LogError("SuperAdmin:Password is not configured — cannot create the Super Admin.");
                return;
            }
            db.AdminAccounts.Add(new AdminAccount
            {
                Email = _superAdmin.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(_superAdmin.Password),
                FullName = _superAdmin.FullName,
                IsActive = true,
                IsSuperAdmin = true
            });
            await db.SaveChangesAsync(ct);
            log.LogWarning("Seeded Super Admin account: {Email}", _superAdmin.Email);
        }
        else if (!existing.IsSuperAdmin)
        {
            // Promote a pre-existing account with this email (created before the feature shipped).
            existing.IsSuperAdmin = true;
            existing.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }
    }

    /// <summary>Item 8: the retired "Classic Wedding" template is deactivated so it leaves the
    /// selector, without deleting rows that historical invitations may reference.</summary>
    private async Task RetireRemovedTemplatesAsync(CancellationToken ct)
    {
        var retired = await db.Templates
            .Where(t => t.Name == "Classic Wedding" && t.IsActive)
            .ToListAsync(ct);
        if (retired.Count == 0) return;
        foreach (var t in retired) t.IsActive = false;
        await db.SaveChangesAsync(ct);
        log.LogInformation("Retired {Count} 'Classic Wedding' template(s).", retired.Count);
    }

    /// <summary>Inserts only the built-in templates that are missing (single query, scalable).</summary>
    private async Task SeedTemplatesAsync(CancellationToken ct)
    {
        var existing = (await db.Templates.Where(t => t.IsBuiltin).Select(t => t.Name).ToListAsync(ct))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var toAdd = BuiltinTemplates.All.Where(t => !existing.Contains(t.Name)).ToList();
        if (toAdd.Count == 0) return;

        var adminId = await db.AdminAccounts.OrderBy(a => a.CreatedAt).Select(a => a.Id).FirstAsync(ct);
        foreach (var t in toAdd)
        {
            db.Templates.Add(new Template
            {
                CreatedBy = adminId,
                Name = t.Name,
                Description = t.Description,
                EventType = t.EventType,
                IsBuiltin = true,
                IsActive = true,
                Data = t.Data
            });
        }
        await db.SaveChangesAsync(ct);
        log.LogInformation("Seeded {Count} built-in template(s): {Names}",
            toAdd.Count, string.Join(", ", toAdd.Select(t => t.Name)));
    }

    /// <summary>Gives pre-slug guests a name-based slug so their personal links keep working.</summary>
    private async Task BackfillGuestSlugsAsync(CancellationToken ct)
    {
        var slugless = await db.Guests.Where(g => g.Slug == "" || g.Slug == null).ToListAsync(ct);
        if (slugless.Count == 0) return;

        var taken = (await db.Guests.Select(g => g.Slug).ToListAsync(ct))
            .Where(s => !string.IsNullOrEmpty(s)).ToHashSet();
        foreach (var g in slugless)
        {
            var baseSlug = SlugHelper.Slugify(g.Name);
            var candidate = baseSlug;
            var n = 2;
            while (!taken.Add(candidate)) candidate = $"{baseSlug}-{n++}";
            g.Slug = candidate;
        }
        await db.SaveChangesAsync(ct);
        log.LogInformation("Backfilled name-based slugs for {Count} guest(s).", slugless.Count);
    }
}
