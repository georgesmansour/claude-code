using System.Security.Claims;
using InvitationPlatform.Api.Auth;
using InvitationPlatform.Api.Controllers;
using InvitationPlatform.Api.Services.Media;
using InvitationPlatform.Api.Services.Storage;
using InvitationPlatform.Domain.Entities;
using InvitationPlatform.Domain.Enums;
using InvitationPlatform.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace InvitationPlatform.Tests;

/// <summary>
/// Shared helpers for spinning up an isolated in-memory database and wiring the
/// controllers with the minimal HTTP context they need in unit tests.
/// </summary>
internal static class TestSupport
{
    public static AppDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            // Unique name per context so tests never share state.
            .UseInMemoryDatabase($"tests-{Guid.NewGuid()}")
            .EnableSensitiveDataLogging()
            .Options;
        return new AppDbContext(options);
    }

    /// <summary>Seeds a published invitation and returns it.</summary>
    public static Invitation SeedInvitation(AppDbContext db, string slug = "sofia-and-marc", int maxAttendees = 10)
    {
        var inv = new Invitation
        {
            Id = Guid.NewGuid(),
            CreatedBy = Guid.NewGuid(),
            Slug = slug,
            Title = "Sofia & Marc",
            Status = InvitationStatus.Published,
            MaxAttendees = maxAttendees,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Invitations.Add(inv);
        db.SaveChanges();
        return inv;
    }

    public static Guest SeedGuest(AppDbContext db, Guid invitationId, string name, string slug,
        int maxAttendees = 4, string token = "tok-abc")
    {
        var guest = new Guest
        {
            Id = Guid.NewGuid(),
            InvitationId = invitationId,
            Name = name,
            Slug = slug,
            Token = token,
            MaxAttendees = maxAttendees,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Guests.Add(guest);
        db.SaveChanges();
        return guest;
    }

    public static AdminAccount SeedAdmin(AppDbContext db, string email, string password,
        bool isSuperAdmin = false, bool isActive = true)
    {
        var admin = new AdminAccount
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            FullName = isSuperAdmin ? "Super Admin" : "Admin",
            IsActive = isActive,
            IsSuperAdmin = isSuperAdmin,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.AdminAccounts.Add(admin);
        db.SaveChanges();
        return admin;
    }

    /// <summary>AuthController wired with a real JWT service (test signing key).</summary>
    public static AuthController NewAuthController(AppDbContext db)
    {
        var jwt = new JwtTokenService(new JwtSettings
        {
            Key = "test-signing-key-must-be-long-enough-for-hmac-sha256-0123456789"
        });
        return new AuthController(db, jwt);
    }

    public static MediaService NewMediaService(AppDbContext db, IFileStorage? storage = null)
        => new(db, storage ?? new FakeFileStorage(), Options.Create(new MediaOptions()),
               NullLogger<MediaService>.Instance);

    /// <summary>PublicController with a usable HttpContext (IP + headers available).</summary>
    public static PublicController NewPublicController(AppDbContext db)
    {
        var ctrl = new PublicController(db, NewMediaService(db));
        ctrl.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        return ctrl;
    }

    /// <summary>AdminController authenticated as a Super Admin.</summary>
    public static AdminController NewAdminController(AppDbContext db, Guid adminId)
    {
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, adminId.ToString()),
            new Claim(ClaimTypes.Role, "SuperAdmin")
        }, "TestAuth");
        return new AdminController(db)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            }
        };
    }

    /// <summary>ClientController authenticated as the owner of <paramref name="invitationId"/>.</summary>
    public static ClientController NewClientController(AppDbContext db, Guid invitationId)
    {
        var identity = new ClaimsIdentity(new[]
        {
            new Claim("invitation_id", invitationId.ToString()),
            new Claim(ClaimTypes.Role, "Client")
        }, "TestAuth");

        var ctrl = new ClientController(db);
        ctrl.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
        return ctrl;
    }

    public static T Body<T>(IActionResult result) where T : class
    {
        var ok = Assert.IsType<OkObjectResult>(result);
        return Assert.IsType<T>(ok.Value!);
    }
}

/// <summary>In-memory <see cref="IFileStorage"/> for tests — no disk touched.</summary>
internal sealed class FakeFileStorage : IFileStorage
{
    private readonly Dictionary<string, byte[]> _files = new();
    public int Count => _files.Count;

    public Task SaveAsync(string relativePath, Stream content, CancellationToken ct = default)
    {
        using var ms = new MemoryStream();
        content.CopyTo(ms);
        _files[relativePath] = ms.ToArray();
        return Task.CompletedTask;
    }

    public Task<Stream?> OpenReadAsync(string relativePath, CancellationToken ct = default)
        => Task.FromResult<Stream?>(_files.TryGetValue(relativePath, out var b) ? new MemoryStream(b) : null);

    public Task<bool> DeleteAsync(string relativePath, CancellationToken ct = default)
        => Task.FromResult(_files.Remove(relativePath));

    public Task<bool> ExistsAsync(string relativePath, CancellationToken ct = default)
        => Task.FromResult(_files.ContainsKey(relativePath));
}
