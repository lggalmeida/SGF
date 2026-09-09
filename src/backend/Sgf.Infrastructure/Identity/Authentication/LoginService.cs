using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Sgf.Application.Identity;
using Sgf.Domain.Companies;
using Sgf.Infrastructure.Database;

namespace Sgf.Infrastructure.Identity.Authentication;

public sealed class LoginService : ILoginUseCase
{
    private static readonly LoginError InvalidCredentialsError = new(
        LoginErrorCode.InvalidCredentials,
        "Invalid credentials.");

    private readonly SgfDbContext _dbContext;
    private readonly JwtOptions _jwtOptions;
    private readonly UserManager<ApplicationUser> _userManager;

    public LoginService(
        SgfDbContext dbContext,
        IOptions<JwtOptions> jwtOptions,
        UserManager<ApplicationUser> userManager)
    {
        _dbContext = dbContext;
        _jwtOptions = jwtOptions.Value;
        _userManager = userManager;
    }

    public async Task<LoginResult> ExecuteAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return LoginResult.Failure(InvalidCredentialsError);
        }

        var user = await _userManager.FindByEmailAsync(request.Email.Trim());

        if (user is null)
        {
            return LoginResult.Failure(InvalidCredentialsError);
        }

        var passwordIsValid = await _userManager.CheckPasswordAsync(user, request.Password);

        if (!passwordIsValid)
        {
            return LoginResult.Failure(InvalidCredentialsError);
        }

        var memberships = await _dbContext.Memberships
            .AsNoTracking()
            .Include(membership => membership.Company)
            .Where(membership =>
                membership.UserId == user.Id
                && membership.IsActive
                && membership.Company.IsActive)
            .ToListAsync(cancellationToken);

        if (memberships.Count == 0)
        {
            return LoginResult.Failure(new LoginError(
                LoginErrorCode.NoActiveCompany,
                "The account has no active company available.",
                "no_active_company"));
        }

        if (memberships.Count > 1)
        {
            return LoginResult.Failure(new LoginError(
                LoginErrorCode.CompanySelectionRequired,
                "Company selection is required.",
                "company_selection_required"));
        }

        if (!IsJwtConfigurationValid())
        {
            return LoginResult.Failure(new LoginError(
                LoginErrorCode.ConfigurationInvalid,
                "Authentication is not configured correctly."));
        }

        var membership = memberships.Single();
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(_jwtOptions.AccessTokenMinutes);
        var accessToken = CreateAccessToken(user, membership, expiresAt);

        return LoginResult.Success(new LoginResponse(
            accessToken,
            "Bearer",
            expiresAt,
            user.Id,
            membership.CompanyId,
            membership.Role.ToString()));
    }

    private string CreateAccessToken(ApplicationUser user, Membership membership, DateTimeOffset expiresAt)
    {
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

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private bool IsJwtConfigurationValid()
    {
        return !string.IsNullOrWhiteSpace(_jwtOptions.Issuer)
            && !string.IsNullOrWhiteSpace(_jwtOptions.Audience)
            && !string.IsNullOrWhiteSpace(_jwtOptions.SigningKey)
            && Encoding.UTF8.GetByteCount(_jwtOptions.SigningKey) >= 32
            && _jwtOptions.AccessTokenMinutes > 0;
    }
}

