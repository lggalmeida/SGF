using Microsoft.AspNetCore.Authorization;
using Sgf.Application.Identity;
using Sgf.Domain.Companies;

namespace Sgf.Api.Authorization;

public sealed class CurrentTenantRoleRequirement : IAuthorizationRequirement
{
    public CurrentTenantRoleRequirement(params MembershipRole[] allowedRoles)
    {
        AllowedRoles = allowedRoles.ToHashSet();
    }

    public IReadOnlySet<MembershipRole> AllowedRoles { get; }
}

public sealed class CurrentTenantRoleAuthorizationHandler
    : AuthorizationHandler<CurrentTenantRoleRequirement>
{
    private readonly ICurrentUserAuthorizationService _authorizationService;

    public CurrentTenantRoleAuthorizationHandler(ICurrentUserAuthorizationService authorizationService)
    {
        _authorizationService = authorizationService;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        CurrentTenantRoleRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return;
        }

        if (await _authorizationService.HasAnyRoleAsync(requirement.AllowedRoles))
        {
            context.Succeed(requirement);
        }
    }
}
