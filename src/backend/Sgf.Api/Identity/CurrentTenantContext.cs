using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Sgf.Application.Identity;
using Sgf.Domain.Companies;
using Sgf.Infrastructure.Database;
using Sgf.Infrastructure.Identity;
using Sgf.Infrastructure.Identity.Authentication;

namespace Sgf.Api.Identity;

public sealed class CurrentTenantContext : ICurrentTenantContext
{
    private readonly SgfDbContext _dbContext;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly UserManager<ApplicationUser> _userManager;
    private Task<CurrentTenant?>? _currentTenantTask;

    public CurrentTenantContext(
        SgfDbContext dbContext,
        IHttpContextAccessor httpContextAccessor,
        UserManager<ApplicationUser> userManager)
    {
        _dbContext = dbContext;
        _httpContextAccessor = httpContextAccessor;
        _userManager = userManager;
    }

    public Task<CurrentTenant?> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        _currentTenantTask ??= ResolveCurrentTenantAsync(cancellationToken);

        return _currentTenantTask;
    }

    private async Task<CurrentTenant?> ResolveCurrentTenantAsync(CancellationToken cancellationToken)
    {
        var principal = _httpContextAccessor.HttpContext?.User;

        if (principal?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var userId = principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
        var companyIdValue = principal.FindFirstValue(SgfClaimTypes.CompanyId);
        var roleValue = principal.FindFirstValue(ClaimTypes.Role);

        if (string.IsNullOrWhiteSpace(userId)
            || string.IsNullOrWhiteSpace(roleValue)
            || !Guid.TryParse(companyIdValue, out var companyId)
            || !Enum.TryParse<MembershipRole>(roleValue, ignoreCase: false, out var tokenRole))
        {
            return null;
        }

        var user = await _userManager.FindByIdAsync(userId);

        if (user is null)
        {
            return null;
        }

        var membership = await _dbContext.Memberships
            .AsNoTracking()
            .Include(item => item.Company)
            .SingleOrDefaultAsync(item =>
                item.UserId == userId
                && item.CompanyId == companyId
                && item.IsActive
                && item.Company.IsActive,
                cancellationToken);

        if (membership is null || membership.Role != tokenRole)
        {
            return null;
        }

        _dbContext.UseCurrentCompany(membership.CompanyId);

        return new CurrentTenant(
            user.Id,
            membership.CompanyId,
            membership.Role,
            user.Name,
            user.Email ?? string.Empty,
            membership.Company.Name);
    }
}

