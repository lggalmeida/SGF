namespace Sgf.Application.Identity;

public sealed class LoginResult
{
    private LoginResult(LoginResponse? value, LoginError? error)
    {
        Value = value;
        Error = error;
    }

    public bool Succeeded => Error is null;

    public LoginResponse? Value { get; }

    public LoginError? Error { get; }

    public static LoginResult Success(LoginResponse value) => new(value, null);

    public static LoginResult Failure(LoginError error) => new(null, error);
}
