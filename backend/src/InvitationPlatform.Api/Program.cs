using Scalar.AspNetCore;
using System.Security.Cryptography;
using System.Text;
using InvitationPlatform.Api.Auth;
using InvitationPlatform.Domain.Entities;
using InvitationPlatform.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Rewrite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

// ── JWT key — loaded from DB, generated once on first boot ────────────
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("DefaultConnection is not configured.");

string jwtKey;
{
    await using var conn = new NpgsqlConnection(connectionString);
    await conn.OpenAsync();

    await using var setup = conn.CreateCommand();
    setup.CommandText = """
        CREATE TABLE IF NOT EXISTS system_settings (
            key         VARCHAR(100) PRIMARY KEY,
            value       TEXT        NOT NULL,
            updated_at  TIMESTAMPTZ NOT NULL DEFAULT NOW()
        );
        """;
    await setup.ExecuteNonQueryAsync();

    await using var sel = conn.CreateCommand();
    sel.CommandText = "SELECT value FROM system_settings WHERE key = 'jwt_signing_key'";
    var existing = await sel.ExecuteScalarAsync() as string;

    if (existing is not null)
    {
        jwtKey = existing;
    }
    else
    {
        jwtKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        await using var ins = conn.CreateCommand();
        ins.CommandText = "INSERT INTO system_settings (key, value) VALUES ('jwt_signing_key', @v)";
        ins.Parameters.AddWithValue("v", jwtKey);
        await ins.ExecuteNonQueryAsync();
        Console.WriteLine("INFO  Generated and persisted new JWT signing key.");
    }
}

// ── Database ──────────────────────────────────────────────────────────
builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseNpgsql(
        connectionString,
        npg => npg.MigrationsAssembly("InvitationPlatform.Infrastructure")));

// ── Auth ─────────────────────────────────────────────────────────────
var jwtSettings = new JwtSettings
{
    Key           = jwtKey,
    Issuer        = builder.Configuration["Jwt:Issuer"]        ?? "InvitationPlatform",
    Audience      = builder.Configuration["Jwt:Audience"]      ?? "InvitationPlatform",
    ExpiryMinutes = int.TryParse(builder.Configuration["Jwt:ExpiryMinutes"], out var em) ? em : 60
};
builder.Services.AddSingleton(jwtSettings);
builder.Services.AddSingleton<JwtTokenService>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer    = jwtSettings.Issuer,
            ValidAudience  = jwtSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });
builder.Services.AddAuthorization();

// ── MVC + OpenAPI ────────────────────────────────────────────────────
builder.Services.AddControllers().AddJsonOptions(o =>
{
    o.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    o.JsonSerializerOptions.DefaultIgnoreCondition =
        System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
});
builder.Services.AddOpenApi();

// ── CORS ─────────────────────────────────────────────────────────────
builder.Services.AddCors(o => o.AddPolicy("Frontend", p => p
    .SetIsOriginAllowed(_ => true)         // simplest for dev; tighten for prod
    .AllowAnyHeader()
    .AllowAnyMethod()));

var app = builder.Build();

// ── Auto-migrate + seed initial admin on startup ─────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();

    if (!await db.AdminAccounts.AnyAsync())
    {
        var seedEmail = builder.Configuration["Seed:AdminEmail"] ?? "admin@invitations.local";
        var seedPassword = builder.Configuration["Seed:AdminPassword"] ?? "Admin123!";
        db.AdminAccounts.Add(new AdminAccount
        {
            Email = seedEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(seedPassword),
            FullName = "Initial Admin",
            IsActive = true
        });
        await db.SaveChangesAsync();
        app.Logger.LogWarning("Seeded initial admin: {Email}  (password: {Password})", seedEmail, seedPassword);
    }

    // Seed builtin templates individually (by name) so new templates reach existing databases too.
    {
        var firstAdminId = await db.AdminAccounts.OrderBy(a => a.CreatedAt).Select(a => a.Id).FirstAsync();

        if (!await db.Templates.AnyAsync(t => t.Name == "Classic Wedding"))
        {
            db.Templates.Add(new Template
            {
                CreatedBy = firstAdminId,
                Name = "Classic Wedding",
                Description = "Elegant gold-accented wedding invitation",
                IsBuiltin = true,
                IsActive = true,
                Data = """
                {
                  "title": "New Invitation",
                  "cover": { "enabled": true, "eventLabel": "Wedding", "names": "Name & Name", "tagline": "Are getting married", "hostText": "Host Families", "hostIntro": "With heartfelt joy,", "hostOutro": "invite you to celebrate", "image": "https://images.unsplash.com/photo-1583939003579-730e3918a45a?w=800&q=80", "buttonText": "Open Invitation" },
                  "countdown": { "enabled": true, "label": "Save the date", "date": "", "description": "Join us as we begin our forever.", "image": "https://images.unsplash.com/photo-1519741497674-611481863552?w=800&q=80" },
                  "locations": { "enabled": true, "label": "The Celebration", "title": "Where & When", "image": "https://images.unsplash.com/photo-1478147427282-58a87a702b70?w=800&q=80", "items": [] },
                  "gifts": { "enabled": false, "label": "Gift Registry", "title": "With Love", "image": "https://images.unsplash.com/photo-1464349095431-e9a21285b5f3?w=800&q=80", "description": "Your love and presence is the greatest gift.", "items": [] },
                  "rsvp": { "enabled": true, "label": "Be Our Guest", "title": "RSVP", "image": "https://images.unsplash.com/photo-1511285560929-80b456fea0bc?w=800&q=80", "deadline": "", "maxPeople": 10 },
                  "customSections": []
                }
                """
            });
        }

        if (!await db.Templates.AnyAsync(t => t.Name == "Elegant Noir"))
        {
            db.Templates.Add(new Template
            {
                CreatedBy = firstAdminId,
                Name = "Elegant Noir",
                Description = "Dark scrolling invitation with script typography, envelope opening, gallery, timeline and music",
                IsBuiltin = true,
                IsActive = true,
                Data = """
                {
                  "title": "New Invitation",
                  "cover": { "enabled": true, "eventLabel": "Wedding", "names": "Name & Name", "tagline": "Are getting married", "greeting": "Dear", "hostText": "", "image": "", "sealImage": "", "buttonText": "Tap to open" },
                  "countdown": { "enabled": true, "label": "Save the date", "date": "", "description": "Venue name, City", "image": "" },
                  "families": { "enabled": true, "label": "Together with their families", "title": "", "items": [] },
                  "gallery": { "enabled": true, "label": "Before forever", "title": "A glimpse of us", "items": [] },
                  "locations": { "enabled": true, "label": "Join us", "title": "The Celebration", "image": "", "items": [] },
                  "timeline": { "enabled": true, "label": "The day", "title": "Wedding Timeline", "items": [] },
                  "gifts": { "enabled": false, "label": "With love", "title": "Gift Registry", "description": "Your presence is the greatest gift. For those who wish, a wedding list is available:", "items": [] },
                  "rsvp": { "enabled": true, "label": "Kindly reply by", "title": "Will you join us?", "deadline": "", "maxPeople": 10, "buttonText": "Send RSVP", "allowWishes": true },
                  "memories": { "enabled": false, "title": "Share Your Memories", "description": "During or after the event, open the link below to share your photos with us", "url": "", "buttonText": "Share Memories" },
                  "music": { "enabled": false, "url": "", "autoplay": true },
                  "customSections": []
                }
                """
            });
        }

        if (!await db.Templates.AnyAsync(t => t.Name == "Serene Beige"))
        {
            db.Templates.Add(new Template
            {
                CreatedBy = firstAdminId,
                Name = "Serene Beige",
                Description = "Light beige scrolling invitation with monogram hero, calendar card, split venue details, timeline, gallery and music",
                IsBuiltin = true,
                IsActive = true,
                Data = """
                {
                  "title": "New Invitation",
                  "cover": { "enabled": true, "eventLabel": "", "names": "Name & Name", "tagline": "Request the honor of your presence at their wedding", "hostIntro": "And the two shall become one", "hostOutro": "Mark 10: 8-9", "image": "", "buttonText": "" },
                  "countdown": { "enabled": true, "label": "Save the date", "date": "", "description": "", "image": "" },
                  "families": { "enabled": true, "label": "", "title": "", "items": [] },
                  "locations": { "enabled": true, "label": "Where & When", "title": "", "image": "", "items": [] },
                  "timeline": { "enabled": true, "label": "The day", "title": "Timeline", "items": [] },
                  "gallery": { "enabled": true, "label": "", "title": "Captured Moments", "items": [] },
                  "gifts": { "enabled": false, "label": "", "title": "Wedding Gift", "description": "Your presence is the best gift. Should you feel inclined, a list is available via Whish Money.", "items": [] },
                  "rsvp": { "enabled": true, "label": "Be our guest", "title": "RSVP", "deadline": "", "maxPeople": 10, "buttonText": "Send Response", "allowWishes": true },
                  "memories": { "enabled": false, "title": "Share Your Memories", "description": "During or after the event, open the link below to share your photos with us", "url": "", "buttonText": "Share Memories" },
                  "music": { "enabled": false, "url": "", "autoplay": true },
                  "customSections": []
                }
                """
            });
        }

        await db.SaveChangesAsync();
    }

    // Backfill name-based slugs for any guests created before slugs existed, so their
    // personal links (and the new /invite/<name> URLs) work without a manual migration.
    var slugless = await db.Guests.Where(g => g.Slug == "" || g.Slug == null).ToListAsync();
    if (slugless.Count > 0)
    {
        var taken = (await db.Guests.Select(g => g.Slug).ToListAsync())
            .Where(s => !string.IsNullOrEmpty(s)).ToHashSet();
        foreach (var g in slugless)
        {
            var baseSlug = InvitationPlatform.Api.Services.SlugHelper.Slugify(g.Name);
            var candidate = baseSlug;
            var n = 2;
            while (!taken.Add(candidate)) candidate = $"{baseSlug}-{n++}";
            g.Slug = candidate;
        }
        await db.SaveChangesAsync();
        app.Logger.LogInformation("Backfilled name-based slugs for {Count} guest(s).", slugless.Count);
    }
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();   // UI at /scalar/v1

    // Walk up 3 directories (Api → src → backend → repo root) to serve the HTML files.
    // Allows opening http://localhost:5000/admin.html without any extra config.
    var repoRoot = builder.Environment.ContentRootPath;
    for (var i = 0; i < 3; i++) repoRoot = Path.GetDirectoryName(repoRoot) ?? repoRoot;

    var frontendDir = Path.Combine(repoRoot, "frontend");
    if (File.Exists(Path.Combine(frontendDir, "index.html")))
    {
        app.UseRewriter(new RewriteOptions()
            // Personal guest links: /invite/<name-slug> → the dispatcher, which reads the slug.
            .AddRewrite(@"^invite/[A-Za-z0-9._~-]+$", "index.html", skipRemainingRules: true)
            // Any extensionless single-segment page path → its .html file
            // (e.g. /serene-beige → /serene-beige.html). New templates need no changes here.
            // "health" is excluded because it's a mapped endpoint, not a page.
            .AddRewrite(@"^(?!health$)([A-Za-z0-9-]+)$", "$1.html", skipRemainingRules: true));

        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(frontendDir),
            RequestPath  = "",
            // HTML and config must always revalidate so a fresh deploy is never masked by a
            // stale cached page (the old "works only after Ctrl+F5" symptom).
            OnPrepareResponse = ctx =>
            {
                var path = ctx.File.Name;
                if (path.EndsWith(".html", StringComparison.OrdinalIgnoreCase) ||
                    path.Equals("config.js", StringComparison.OrdinalIgnoreCase))
                {
                    ctx.Context.Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
                    ctx.Context.Response.Headers.Pragma = "no-cache";
                    ctx.Context.Response.Headers.Expires = "0";
                }
            }
        });
        app.Logger.LogInformation("Serving HTML files from {Root}", frontendDir);
    }
}

app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.MapGet("/health", () => Results.Ok(new { status = "ok", time = DateTime.UtcNow }));

app.Run();
