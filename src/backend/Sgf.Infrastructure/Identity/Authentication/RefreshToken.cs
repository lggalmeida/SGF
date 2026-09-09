namespace Sgf.Infrastructure.Identity.Authentication;

public sealed class RefreshToken
{
    private RefreshToken()
    {
    }

    public RefreshToken(
        string userId,
        Guid companyId,
        string tokenHash,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("User id is required.", nameof(userId));
        }

        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            throw new ArgumentException("Token hash is required.", nameof(tokenHash));
        }

        Id = Guid.NewGuid();
        UserId = userId;
        CompanyId = companyId;
        TokenHash = tokenHash;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }

    public Guid Id { get; private set; }

    public string UserId { get; private set; } = string.Empty;

    public Guid CompanyId { get; private set; }

    public string TokenHash { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public bool IsRevoked => RevokedAt.HasValue;

    public void Revoke(DateTimeOffset revokedAt)
    {
        RevokedAt ??= revokedAt;
    }
}
