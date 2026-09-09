namespace Sgf.Infrastructure.Database.MultiTenancy;

public sealed class TenantContextUnavailableException : InvalidOperationException
{
    public TenantContextUnavailableException()
        : base("A valid company context is required for tenant-scoped data operations.")
    {
    }
}
