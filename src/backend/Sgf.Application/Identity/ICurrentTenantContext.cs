namespace Sgf.Application.Identity;

public interface ICurrentTenantContext
{
    Task<CurrentTenant?> GetCurrentAsync(CancellationToken cancellationToken = default);
}
