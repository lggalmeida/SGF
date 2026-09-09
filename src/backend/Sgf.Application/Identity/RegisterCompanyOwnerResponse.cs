namespace Sgf.Application.Identity;

public sealed record RegisterCompanyOwnerResponse(
    string UserId,
    Guid CompanyId,
    Guid MembershipId,
    string Role);
