namespace Sgf.Application.Identity;

public interface ILoginUseCase
{
    Task<LoginResult> ExecuteAsync(LoginRequest request, CancellationToken cancellationToken = default);
}
