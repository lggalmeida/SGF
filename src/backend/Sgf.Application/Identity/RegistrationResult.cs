namespace Sgf.Application.Identity;

public sealed class RegistrationResult
{
    private RegistrationResult(RegisterCompanyOwnerResponse? value, RegistrationError? error)
    {
        Value = value;
        Error = error;
    }

    public bool Succeeded => Error is null;

    public RegisterCompanyOwnerResponse? Value { get; }

    public RegistrationError? Error { get; }

    public static RegistrationResult Success(RegisterCompanyOwnerResponse value) => new(value, null);

    public static RegistrationResult Failure(RegistrationError error) => new(null, error);
}
