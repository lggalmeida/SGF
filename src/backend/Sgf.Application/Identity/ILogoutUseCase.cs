namespace Sgf.Application.Identity;

public interface ILogoutUseCase
{
    Task ExecuteAsync(LogoutRequest request, CancellationToken cancellationToken = default);
}
