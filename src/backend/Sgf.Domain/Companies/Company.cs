namespace Sgf.Domain.Companies;

public sealed class Company
{
    private readonly List<Membership> _memberships = [];

    private Company()
    {
    }

    public Company(string name, string? tradeName = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Company name is required.", nameof(name));
        }

        Id = Guid.NewGuid();
        Name = name.Trim();
        TradeName = string.IsNullOrWhiteSpace(tradeName) ? null : tradeName.Trim();
        IsActive = true;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string? TradeName { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyCollection<Membership> Memberships => _memberships.AsReadOnly();
}
