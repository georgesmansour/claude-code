using InvitationPlatform.Api.Auth;
using InvitationPlatform.Api.Dtos;
using InvitationPlatform.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InvitationPlatform.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(AppDbContext db, JwtTokenService jwt) : ControllerBase
{
    [HttpPost("admin/login")]
    [AllowAnonymous]
    public async Task<IActionResult> AdminLogin([FromBody] LoginRequest req)
    {
        var admin = await db.AdminAccounts
            .FirstOrDefaultAsync(a => a.Email == req.Email && a.IsActive);

        if (admin is null || !BCrypt.Net.BCrypt.Verify(req.Password, admin.PasswordHash))
            return Unauthorized(new { error = "Invalid credentials" });

        admin.LastLoginAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var token = jwt.CreateToken(admin.Id, admin.Email, "Admin", admin.FullName);
        return Ok(new LoginResponse(token, "Admin", admin.FullName, false));
    }

    [HttpPost("client/login")]
    [AllowAnonymous]
    public async Task<IActionResult> ClientLogin([FromBody] LoginRequest req)
    {
        var client = await db.ClientAccounts
            .FirstOrDefaultAsync(c => c.Email == req.Email && c.IsActive);

        if (client is null || !BCrypt.Net.BCrypt.Verify(req.Password, client.PasswordHash))
            return Unauthorized(new { error = "Invalid credentials" });

        client.LastLoginAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var token = jwt.CreateToken(client.Id, client.Email, "Client", client.FullName, client.InvitationId);
        return Ok(new LoginResponse(token, "Client", client.FullName, client.MustChangePassword));
    }

    [HttpPost("client/change-password")]
    [Authorize(Roles = "Client")]
    public async Task<IActionResult> ChangeClientPassword([FromBody] ChangePasswordRequest req)
    {
        var clientId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        var client = await db.ClientAccounts.FindAsync(clientId);
        if (client is null) return Unauthorized();

        // First-time forced change skips current-password verification
        if (!client.MustChangePassword)
        {
            if (string.IsNullOrEmpty(req.CurrentPassword))
                return BadRequest(new { error = "Current password is required" });
            if (!BCrypt.Net.BCrypt.Verify(req.CurrentPassword, client.PasswordHash))
                return BadRequest(new { error = "Current password is incorrect" });
        }

        if (req.NewPassword.Length < 8)
            return BadRequest(new { error = "New password must be at least 8 characters" });

        client.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword);
        client.MustChangePassword = false;
        await db.SaveChangesAsync();
        return Ok(new { ok = true });
    }

    [HttpPost("client/change-email")]
    [Authorize(Roles = "Client")]
    public async Task<IActionResult> ChangeClientEmail([FromBody] ChangeEmailRequest req)
    {
        var clientId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        var client = await db.ClientAccounts.FindAsync(clientId);
        if (client is null) return Unauthorized();

        if (!BCrypt.Net.BCrypt.Verify(req.CurrentPassword, client.PasswordHash))
            return BadRequest(new { error = "Current password is incorrect" });

        var email = req.NewEmail?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(email)) return BadRequest(new { error = "New email is required" });
        if (await db.ClientAccounts.AnyAsync(c => c.Email == email && c.Id != clientId))
            return BadRequest(new { error = "Email already in use" });

        client.Email = email;
        await db.SaveChangesAsync();
        return Ok(new { ok = true });
    }

    [HttpPost("admin/change-password")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ChangeAdminPassword([FromBody] ChangePasswordRequest req)
    {
        var adminId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        var admin = await db.AdminAccounts.FindAsync(adminId);
        if (admin is null) return Unauthorized();

        if (string.IsNullOrEmpty(req.CurrentPassword))
            return BadRequest(new { error = "Current password is required" });
        if (!BCrypt.Net.BCrypt.Verify(req.CurrentPassword, admin.PasswordHash))
            return BadRequest(new { error = "Current password is incorrect" });
        if (req.NewPassword.Length < 8)
            return BadRequest(new { error = "New password must be at least 8 characters" });

        admin.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword);
        await db.SaveChangesAsync();
        return Ok(new { ok = true });
    }

    [HttpPost("admin/change-email")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ChangeAdminEmail([FromBody] ChangeEmailRequest req)
    {
        var adminId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        var admin = await db.AdminAccounts.FindAsync(adminId);
        if (admin is null) return Unauthorized();

        if (!BCrypt.Net.BCrypt.Verify(req.CurrentPassword, admin.PasswordHash))
            return BadRequest(new { error = "Current password is incorrect" });

        var email = req.NewEmail?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(email)) return BadRequest(new { error = "New email is required" });
        if (await db.AdminAccounts.AnyAsync(a => a.Email == email && a.Id != adminId))
            return BadRequest(new { error = "Email already in use" });

        admin.Email = email;
        await db.SaveChangesAsync();
        return Ok(new { ok = true });
    }
}
