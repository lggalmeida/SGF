namespace Sgf.Application.Identity;

public sealed record RefreshTokenResponse(
    string AccessToken,
    string TokenType,
    DateTimeOffset ExpiresAt,
    string UserId,
    Guid CompanyId,
    string Role);
