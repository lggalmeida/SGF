namespace Sgf.Application.Identity;

public sealed record RegistrationError(
    RegistrationErrorCode Code,
    string Message,
    IReadOnlyCollection<string> Details);
