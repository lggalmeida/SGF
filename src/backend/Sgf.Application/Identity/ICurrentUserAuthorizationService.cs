using Sgf.Domain.Companies;

namespace Sgf.Application.Identity;

public interface ICurrentUserAuthorizationService
{
    Task<bool> HasAnyRoleAsync(IReadOnlySet<MembershipRole> roles, CancellationToken cancellationToken = default);
}
