using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Sgf.Application.Identity;
using Sgf.Infrastructure.Database;

namespace Sgf.Infrastructure.Identity.Authentication;

public sealed class RefreshTokenService : IRefreshTokenUseCase, ILogoutUseCase
{
    private static readonly string InvalidRefreshTokenMessage = "Invalid refresh token.";

    private readonly AccessTokenFactory _accessTokenFactory;
    private readonly SgfDbContext _dbContext;
    private readonly JwtOptions _jwtOptions;
    private readonly RefreshTokenGenerator _refreshTokenGenerator;
    private readonly RefreshTokenOptions _refreshTokenOptions;

    public RefreshTokenService(
        SgfDbContext dbContext,
        IOptions<JwtOptions> jwtOptions,
        IOptions<RefreshTokenOptions> refreshTokenOptions,
        RefreshTokenGenerator refreshTokenGenerator)
    {
        _dbContext = dbContext;
        _jwtOptions = jwtOptions.Value;
        _refreshTokenOptions = refreshTokenOptions.Value;
        _refreshTokenGenerator = refreshTokenGenerator;
        _accessTokenFactory = new AccessTokenFactory(_jwtOptions);
    }

    public async Task<RefreshTokenResult> ExecuteAsync(
        RefreshTokenRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigurationValid() || string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return RefreshTokenResult.Failure(InvalidRefreshTokenMessage);
        }

        var tokenHash = _refreshTokenGenerator.Hash(request.RefreshToken);
        var storedToken = await _dbContext.RefreshTokens
            .SingleOrDefaultAsync(refreshToken => refreshToken.TokenHash == tokenHash, cancellationToken);

        var now = DateTimeOffset.UtcNow;

        if (storedToken is null || storedToken.IsRevoked || storedToken.ExpiresAt <= now)
        {
            return RefreshTokenResult.Failure(InvalidRefreshTokenMessage);
        }

        var user = await _dbContext.Users.SingleOrDefaultAsync(user => user.Id == storedToken.UserId, cancellationToken);

        if (user is null)
        {
            return RefreshTokenResult.Failure(InvalidRefreshTokenMessage);
        }

        var membership = await _dbContext.Memberships
            .Include(item => item.Company)
            .SingleOrDefaultAsync(item =>
                item.UserId == storedToken.UserId
                && item.CompanyId == storedToken.CompanyId
                && item.IsActive
                && item.Company.IsActive,
                cancellationToken);

        if (membership is null)
        {
            return RefreshTokenResult.Failure(InvalidRefreshTokenMessage);
        }

        storedToken.Revoke(now);
        var newRefreshTokenValue = _refreshTokenGenerator.CreateToken();
        var newRefreshToken = new RefreshToken(
            user.Id,
            membership.CompanyId,
            _refreshTokenGenerator.Hash(newRefreshTokenValue),
            now,
            now.AddDays(_refreshTokenOptions.Days));

        _dbContext.RefreshTokens.Add(newRefreshToken);
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            _dbContext.ChangeTracker.Clear();
            return RefreshTokenResult.Failure(InvalidRefreshTokenMessage);
        }

        var accessToken = _accessTokenFactory.Create(user, membership);

        return RefreshTokenResult.Success(
            new RefreshTokenResponse(
                accessToken.AccessToken,
                accessToken.TokenType,
                accessToken.ExpiresAt,
                accessToken.UserId,
                accessToken.CompanyId,
                accessToken.Role),
            newRefreshTokenValue,
            newRefreshToken.ExpiresAt);
    }

    async Task ILogoutUseCase.ExecuteAsync(LogoutRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return;
        }

        var tokenHash = _refreshTokenGenerator.Hash(request.RefreshToken);
        var storedToken = await _dbContext.RefreshTokens
            .SingleOrDefaultAsync(refreshToken => refreshToken.TokenHash == tokenHash, cancellationToken);

        if (storedToken is null || storedToken.IsRevoked)
        {
            return;
        }

        storedToken.Revoke(DateTimeOffset.UtcNow);
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            _dbContext.ChangeTracker.Clear();
        }
    }

    private bool IsConfigurationValid()
    {
        return !string.IsNullOrWhiteSpace(_jwtOptions.Issuer)
            && !string.IsNullOrWhiteSpace(_jwtOptions.Audience)
            && !string.IsNullOrWhiteSpace(_jwtOptions.SigningKey)
            && System.Text.Encoding.UTF8.GetByteCount(_jwtOptions.SigningKey) >= 32
            && _jwtOptions.AccessTokenMinutes > 0
            && _refreshTokenOptions.Days > 0
            && !string.IsNullOrWhiteSpace(_refreshTokenOptions.CookieName);
    }
}

