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

// Opens the first connection, retrying while the database refuses it. This runs before the
// host is built, so an unhandled failure here kills the process outright — which is exactly
// what happens in Docker, where containers start in parallel, and against a serverless
// database that has auto-suspended and needs a moment to wake.
static async Task<NpgsqlConnection> OpenWithRetryAsync(string connectionString, int attempts = 10)
{
    for (var attempt = 1; ; attempt++)
    {
        var conn = new NpgsqlConnection(connectionString);
        try
        {
            await conn.OpenAsync();
            return conn;
        }
        catch (Exception ex) when (attempt < attempts)
        {
            await conn.DisposeAsync();
            var delay = TimeSpan.FromSeconds(Math.Min(attempt * 2, 10));
            Console.WriteLine(
                $"WARN  Database not reachable (attempt {attempt}/{attempts}): {ex.Message} " +
                $"Retrying in {delay.TotalSeconds:0}s.");
            await Task.Delay(delay);
        }
    }
}

string jwtKey;
{
    await using var conn = await OpenWithRetryAsync(connectionString);

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
        npg =>
        {
            npg.MigrationsAssembly("InvitationPlatform.Infrastructure");
            // Retry transient failures instead of returning a 500. Pooled connections die
            // whenever the database restarts — which happens on every deployment that
            // recreates the db container, and whenever a serverless database auto-suspends.
            // Safe here because nothing in the codebase opens an explicit transaction; those
            // would have to be wrapped in the execution strategy by hand.
            npg.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(5),
                                     errorCodesToAdd: null);
        }));

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
// In every supported setup the pages and the API share an origin — nginx proxies /api/* in
// the container stack, and the API serves the pages itself under "dotnet run" — so production
// needs no cross-origin grant at all, and gets none. Same-origin requests are unaffected by
// an empty policy; only a genuinely cross-origin caller would be refused.
//
// Cors:AllowedOrigins exists for the one case that would need it: hosting the frontend on a
// separate domain. Development keeps the old allow-anything behaviour so a page opened from
// a different dev server still works.
var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(o => o.AddPolicy("Frontend", p =>
{
    if (corsOrigins.Length > 0)
        p.WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod();
    else if (builder.Environment.IsDevelopment())
        p.SetIsOriginAllowed(_ => true).AllowAnyHeader().AllowAnyMethod();
}));

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
        // what the nginx web container does in the container stack. Must run before UseStaticFiles.
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
