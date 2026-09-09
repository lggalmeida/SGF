using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Sgf.Application.Identity;
using Sgf.Infrastructure.Database;

namespace Sgf.Infrastructure.Identity.Authentication;

public sealed class LoginService : ILoginUseCase
{
    private static readonly LoginError InvalidCredentialsError = new(
        LoginErrorCode.InvalidCredentials,
        "Invalid credentials.");

    private readonly AccessTokenFactory _accessTokenFactory;
    private readonly SgfDbContext _dbContext;
    private readonly JwtOptions _jwtOptions;
    private readonly RefreshTokenGenerator _refreshTokenGenerator;
    private readonly RefreshTokenOptions _refreshTokenOptions;
    private readonly UserManager<ApplicationUser> _userManager;

    public LoginService(
        SgfDbContext dbContext,
        IOptions<JwtOptions> jwtOptions,
        IOptions<RefreshTokenOptions> refreshTokenOptions,
        RefreshTokenGenerator refreshTokenGenerator,
        UserManager<ApplicationUser> userManager)
    {
        _dbContext = dbContext;
        _jwtOptions = jwtOptions.Value;
        _refreshTokenOptions = refreshTokenOptions.Value;
        _refreshTokenGenerator = refreshTokenGenerator;
        _userManager = userManager;
        _accessTokenFactory = new AccessTokenFactory(_jwtOptions);
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

        if (!IsConfigurationValid())
        {
            return LoginResult.Failure(new LoginError(
                LoginErrorCode.ConfigurationInvalid,
                "Authentication is not configured correctly."));
        }

        var membership = memberships.Single();
        var accessToken = _accessTokenFactory.Create(user, membership);
        var refreshTokenValue = _refreshTokenGenerator.CreateToken();
        var now = DateTimeOffset.UtcNow;
        var refreshToken = new RefreshToken(
            user.Id,
            membership.CompanyId,
            _refreshTokenGenerator.Hash(refreshTokenValue),
            now,
            now.AddDays(_refreshTokenOptions.Days));

        _dbContext.RefreshTokens.Add(refreshToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return LoginResult.Success(
            new LoginResponse(
                accessToken.AccessToken,
                accessToken.TokenType,
                accessToken.ExpiresAt,
                accessToken.UserId,
                accessToken.CompanyId,
                accessToken.Role),
            refreshTokenValue,
            refreshToken.ExpiresAt);
    }

    private bool IsConfigurationValid()
    {
        return !string.IsNullOrWhiteSpace(_jwtOptions.Issuer)
            && !string.IsNullOrWhiteSpace(_jwtOptions.Audience)
            && !string.IsNullOrWhiteSpace(_jwtOptions.SigningKey)
            && Encoding.UTF8.GetByteCount(_jwtOptions.SigningKey) >= 32
            && _jwtOptions.AccessTokenMinutes > 0
            && _refreshTokenOptions.Days > 0
            && !string.IsNullOrWhiteSpace(_refreshTokenOptions.CookieName);
    }
}
