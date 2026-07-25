using System.Security.Claims;
using InvitationPlatform.Api.Auth;
using InvitationPlatform.Api.Controllers;
using InvitationPlatform.Domain.Entities;
using InvitationPlatform.Domain.Enums;
using InvitationPlatform.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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

    /// <summary>PublicController with a usable HttpContext (IP + headers available).</summary>
    public static PublicController NewPublicController(AppDbContext db)
    {
        var ctrl = new PublicController(db);
        ctrl.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        return ctrl;
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
