using System.IdentityModel.Tokens.Jwt;
using System.Reflection;
using InvitationPlatform.Api.Controllers;
using InvitationPlatform.Api.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvitationPlatform.Tests;

/// <summary>
/// Super Admin authorization: only the Super Admin account may reach the /admin section.
/// Login must hand out the elevated "SuperAdmin" role only to super admins, and the
/// AdminController must be gated on that role (enforced server-side by the framework).
/// </summary>
public class SuperAdminAuthorizationTests
{
    [Fact]
    public async Task Super_admin_login_issues_SuperAdmin_role()
    {
        using var db = TestSupport.NewDb();
        TestSupport.SeedAdmin(db, "superadmin@test.local", "test-password-123", isSuperAdmin: true);
        var ctrl = TestSupport.NewAuthController(db);

        var result = await ctrl.AdminLogin(new LoginRequest("superadmin@test.local", "test-password-123"));

        var body = TestSupport.Body<LoginResponse>(result);
        Assert.Equal("SuperAdmin", body.Role);

        // The signed JWT must carry the SuperAdmin role claim the [Authorize] filter checks.
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(body.Token);
        Assert.Contains(jwt.Claims, c =>
            (c.Type == "role" || c.Type.EndsWith("/role")) && c.Value == "SuperAdmin");
    }

    [Fact]
    public async Task Regular_admin_login_issues_only_the_Admin_role()
    {
        using var db = TestSupport.NewDb();
        TestSupport.SeedAdmin(db, "planner@invitations.local", "Planner123!", isSuperAdmin: false);
        var ctrl = TestSupport.NewAuthController(db);

        var result = await ctrl.AdminLogin(new LoginRequest("planner@invitations.local", "Planner123!"));

        var body = TestSupport.Body<LoginResponse>(result);
        Assert.Equal("Admin", body.Role);
        Assert.NotEqual("SuperAdmin", body.Role);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(body.Token);
        Assert.DoesNotContain(jwt.Claims, c => c.Value == "SuperAdmin");
    }

    [Fact]
    public async Task Login_with_wrong_password_is_unauthorized()
    {
        using var db = TestSupport.NewDb();
        TestSupport.SeedAdmin(db, "superadmin@test.local", "test-password-123", isSuperAdmin: true);
        var ctrl = TestSupport.NewAuthController(db);

        var result = await ctrl.AdminLogin(new LoginRequest("superadmin@test.local", "wrong-password"));

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task Inactive_super_admin_cannot_log_in()
    {
        using var db = TestSupport.NewDb();
        TestSupport.SeedAdmin(db, "superadmin@test.local", "test-password-123", isSuperAdmin: true, isActive: false);
        var ctrl = TestSupport.NewAuthController(db);

        var result = await ctrl.AdminLogin(new LoginRequest("superadmin@test.local", "test-password-123"));

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task Password_is_hashed_not_stored_in_plaintext()
    {
        using var db = TestSupport.NewDb();
        var admin = TestSupport.SeedAdmin(db, "superadmin@test.local", "test-password-123", isSuperAdmin: true);

        Assert.NotEqual("test-password-123", admin.PasswordHash);
        Assert.StartsWith("$2", admin.PasswordHash);          // BCrypt hash marker
        Assert.True(BCrypt.Net.BCrypt.Verify("test-password-123", admin.PasswordHash));
    }

    [Fact]
    public void AdminController_is_gated_on_the_SuperAdmin_role()
    {
        // Guards the core requirement: the whole /admin surface must require SuperAdmin.
        var authorize = typeof(AdminController)
            .GetCustomAttributes<AuthorizeAttribute>(inherit: true)
            .SingleOrDefault();

        Assert.NotNull(authorize);
        Assert.Equal("SuperAdmin", authorize!.Roles);
    }

    [Fact]
    public void AdminController_has_no_AllowAnonymous_escape_hatches()
    {
        // No action may opt out of the SuperAdmin gate.
        var anonEndpoints = typeof(AdminController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.GetCustomAttribute<AllowAnonymousAttribute>() != null)
            .Select(m => m.Name)
            .ToList();

        Assert.Empty(anonEndpoints);
    }

    [Fact]
    public void Admin_self_service_endpoints_allow_admin_and_super_admin()
    {
        // The Super Admin must still be able to change their own password/email from the admin page.
        foreach (var name in new[] { nameof(AuthController.ChangeAdminPassword), nameof(AuthController.ChangeAdminEmail) })
        {
            var attr = typeof(AuthController).GetMethod(name)!
                .GetCustomAttribute<AuthorizeAttribute>();
            Assert.NotNull(attr);
            Assert.Contains("SuperAdmin", attr!.Roles);
            Assert.Contains("Admin", attr.Roles);
        }
    }
}
