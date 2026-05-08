using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace InvitationPlatform.Api.Auth;

public class JwtTokenService(IConfiguration config)
{
    public string CreateToken(Guid userId, string email, string role, string? fullName, Guid? invitationId = null)
    {
        var key   = config["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key missing");
        var iss   = config["Jwt:Issuer"]   ?? "InvitationPlatform";
        var aud   = config["Jwt:Audience"] ?? "InvitationPlatform";
        var mins  = int.TryParse(config["Jwt:ExpiryMinutes"], out var m) ? m : 60;

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Email, email),
            new(ClaimTypes.Role, role),
            new("name", fullName ?? "")
        };
        if (invitationId.HasValue)
            claims.Add(new("invitation_id", invitationId.Value.ToString()));

        var creds = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: iss, audience: aud,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(mins),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
