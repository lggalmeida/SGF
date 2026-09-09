namespace Sgf.Application.Identity;

public sealed class RefreshTokenResult
{
    private RefreshTokenResult(
        RefreshTokenResponse? value,
        string? error,
        string? refreshToken,
        DateTimeOffset? refreshTokenExpiresAt)
    {
        Value = value;
        Error = error;
        RefreshToken = refreshToken;
        RefreshTokenExpiresAt = refreshTokenExpiresAt;
    }

    public bool Succeeded => Error is null;

    public RefreshTokenResponse? Value { get; }

    public string? Error { get; }

    public string? RefreshToken { get; }

    public DateTimeOffset? RefreshTokenExpiresAt { get; }

    public static RefreshTokenResult Success(
        RefreshTokenResponse value,
        string refreshToken,
        DateTimeOffset refreshTokenExpiresAt) => new(value, null, refreshToken, refreshTokenExpiresAt);

    public static RefreshTokenResult Failure(string error) => new(null, error, null, null);
}
