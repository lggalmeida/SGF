namespace Sgf.Application.Identity;

public sealed record LoginError(
    LoginErrorCode Code,
    string Message,
    string? PublicCode = null);
