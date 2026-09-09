using Sgf.Application.Identity;
using Sgf.Domain.Companies;

namespace Sgf.Infrastructure.Identity.Authentication;

public sealed class CurrentUserAuthorizationService : ICurrentUserAuthorizationService
{
    private readonly ICurrentTenantContext _currentTenantContext;

    public CurrentUserAuthorizationService(ICurrentTenantContext currentTenantContext)
    {
        _currentTenantContext = currentTenantContext;
    }

    public async Task<bool> HasAnyRoleAsync(
        IReadOnlySet<MembershipRole> roles,
        CancellationToken cancellationToken = default)
    {
        var currentTenant = await _currentTenantContext.GetCurrentAsync(cancellationToken);

        return currentTenant is not null && roles.Contains(currentTenant.Role);
    }
}
