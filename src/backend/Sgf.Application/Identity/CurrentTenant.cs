using Sgf.Domain.Companies;

namespace Sgf.Application.Identity;

public sealed record CurrentTenant(
    string UserId,
    Guid CompanyId,
    MembershipRole Role,
    string UserName,
    string UserEmail,
    string CompanyName);
