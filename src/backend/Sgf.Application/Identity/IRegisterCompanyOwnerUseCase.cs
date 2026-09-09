namespace Sgf.Application.Identity;

public interface IRegisterCompanyOwnerUseCase
{
    Task<RegistrationResult> ExecuteAsync(
        RegisterCompanyOwnerRequest request,
        CancellationToken cancellationToken = default);
}
