namespace Sgf.Domain.Companies;

public sealed class Membership
{
    private Membership()
    {
    }

    public Membership(string userId, Guid companyId, MembershipRole role)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("User id is required.", nameof(userId));
        }

        if (!Enum.IsDefined(role))
        {
            throw new ArgumentOutOfRangeException(nameof(role), role, "Membership role is invalid.");
        }

        Id = Guid.NewGuid();
        UserId = userId;
        CompanyId = companyId;
        Role = role;
        IsActive = true;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }

    public string UserId { get; private set; } = string.Empty;

    public Guid CompanyId { get; private set; }

    public MembershipRole Role { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public Company Company { get; private set; } = null!;
}
