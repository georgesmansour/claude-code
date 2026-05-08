using System.Text;
using InvitationPlatform.Api.Auth;
using InvitationPlatform.Domain.Entities;
using InvitationPlatform.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// ── Database ──────────────────────────────────────────────────────────
builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        npg => npg.MigrationsAssembly("InvitationPlatform.Infrastructure")));

// ── Auth ─────────────────────────────────────────────────────────────
builder.Services.AddSingleton<JwtTokenService>();

var jwtKey = builder.Configuration["Jwt:Key"] ?? "dev-secret-change-me-min-32-chars-please";
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
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "InvitationPlatform",
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "InvitationPlatform",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });
builder.Services.AddAuthorization();

// ── MVC + Swagger ────────────────────────────────────────────────────
builder.Services.AddControllers().AddJsonOptions(o =>
{
    o.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    o.JsonSerializerOptions.DefaultIgnoreCondition =
        System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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

    if (!await db.Templates.AnyAsync())
    {
        var firstAdminId = await db.AdminAccounts.OrderBy(a => a.CreatedAt).Select(a => a.Id).FirstAsync();
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
        await db.SaveChangesAsync();
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.MapGet("/health", () => Results.Ok(new { status = "ok", time = DateTime.UtcNow }));

app.Run();
