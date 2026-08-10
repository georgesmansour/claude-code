using Scalar.AspNetCore;
using System.Security.Cryptography;
using System.Text;
using InvitationPlatform.Api.Auth;
using InvitationPlatform.Api.Services.Email;
using InvitationPlatform.Api.Services.Media;
using InvitationPlatform.Api.Services.Seeding;
using InvitationPlatform.Api.Services.Storage;
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

// ── Media storage ────────────────────────────────────────────────────
builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection("Storage"));
builder.Services.Configure<MediaOptions>(builder.Configuration.GetSection("Media"));
builder.Services.AddSingleton<IFileStorage, LocalFileStorage>();
builder.Services.AddScoped<MediaService>();

// ── Email (landing-page demo requests) ───────────────────────────────
builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection("Smtp"));
builder.Services.AddHttpClient();                 // used to fetch OAuth2 tokens
builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();

// ── Seeding (Super Admin credentials from config via the Options pattern) ─────
builder.Services.Configure<SuperAdminSettings>(builder.Configuration.GetSection("SuperAdmin"));
builder.Services.AddScoped<DatabaseSeeder>();

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

    // All idempotent seeding (Super Admin from config, built-in templates, guest-slug backfill)
    // lives in DatabaseSeeder — see Services/Seeding.
    await scope.ServiceProvider.GetRequiredService<DatabaseSeeder>().SeedAsync();
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
            .AddRewrite(@"^invite/[A-Za-z0-9._~-]+$", "invitation.html", skipRemainingRules: true)
            // Template folder without a file: /templates/wedding/elegant-noir → its index.html.
            .AddRewrite(@"^(templates/[A-Za-z0-9-]+/[A-Za-z0-9-]+)/?$", "$1/index.html", skipRemainingRules: true)
            // Any extensionless single-segment page path → its .html file
            // (e.g. /admin → /admin.html). "health" is excluded because it's a mapped endpoint.
            .AddRewrite(@"^(?!health$)([A-Za-z0-9-]+)$", "$1.html", skipRemainingRules: true));

        // Serve the landing page at "/" (and index.html inside any template folder), matching
        // what static hosts like Netlify do by default. Must run before UseStaticFiles.
        app.UseDefaultFiles(new DefaultFilesOptions
        {
            FileProvider = new PhysicalFileProvider(frontendDir),
            RequestPath  = ""
        });

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
