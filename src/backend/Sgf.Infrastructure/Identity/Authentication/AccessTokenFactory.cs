using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Sgf.Domain.Companies;

namespace Sgf.Infrastructure.Identity.Authentication;

public sealed class AccessTokenFactory
{
    private readonly JwtOptions _jwtOptions;

    public AccessTokenFactory(JwtOptions jwtOptions)
    {
        _jwtOptions = jwtOptions;
    }

    public AccessTokenData Create(ApplicationUser user, Membership membership)
    {
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(_jwtOptions.AccessTokenMinutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
            new(SgfClaimTypes.CompanyId, membership.CompanyId.ToString()),
            new(ClaimTypes.Role, membership.Role.ToString())
        };

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.SigningKey));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _jwtOptions.Issuer,
            audience: _jwtOptions.Audience,
            claims: claims,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        return new AccessTokenData(
            new JwtSecurityTokenHandler().WriteToken(token),
            "Bearer",
            expiresAt,
            user.Id,
            membership.CompanyId,
            membership.Role.ToString());
    }
}

public sealed record AccessTokenData(
    string AccessToken,
    string TokenType,
    DateTimeOffset ExpiresAt,
    string UserId,
    Guid CompanyId,
    string Role);
