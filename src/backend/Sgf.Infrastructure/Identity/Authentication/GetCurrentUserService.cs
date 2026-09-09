using Sgf.Application.Identity;

namespace Sgf.Infrastructure.Identity.Authentication;

public sealed class GetCurrentUserService : IGetCurrentUserUseCase
{
    private readonly ICurrentTenantContext _currentTenantContext;

    public GetCurrentUserService(ICurrentTenantContext currentTenantContext)
    {
        _currentTenantContext = currentTenantContext;
    }

    public async Task<CurrentUserResponse?> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var currentTenant = await _currentTenantContext.GetCurrentAsync(cancellationToken);

        if (currentTenant is null)
        {
            return null;
        }

        return new CurrentUserResponse(
            currentTenant.UserId,
            currentTenant.UserName,
            currentTenant.UserEmail,
            new CurrentCompanyResponse(currentTenant.CompanyId, currentTenant.CompanyName),
            currentTenant.Role.ToString());
    }
}
