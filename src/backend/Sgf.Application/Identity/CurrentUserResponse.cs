namespace Sgf.Application.Identity;

public sealed record CurrentUserResponse(
    string UserId,
    string Name,
    string Email,
    CurrentCompanyResponse Company,
    string Role);
