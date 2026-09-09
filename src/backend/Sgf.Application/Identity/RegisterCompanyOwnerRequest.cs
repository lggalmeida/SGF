namespace Sgf.Application.Identity;

public sealed record RegisterCompanyOwnerRequest(
    string Name,
    string Email,
    string Password,
    string CompanyName);
