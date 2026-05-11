using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace InvitationPlatform.Api.Auth;

public class JwtSettings
{
    public required string Key { get; init; }
    public string Issuer { get; init; } = "InvitationPlatform";
    public string Audience { get; init; } = "InvitationPlatform";
    public int ExpiryMinutes { get; init; } = 60;
}

public class JwtTokenService(JwtSettings settings)
{
    public string CreateToken(Guid userId, string email, string role, string? fullName, Guid? invitationId = null)
    {
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
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.Key)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: settings.Issuer,
            audience: settings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(settings.ExpiryMinutes),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
