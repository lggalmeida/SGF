namespace Sgf.Application.Identity;

public interface IGetCurrentUserUseCase
{
    Task<CurrentUserResponse?> ExecuteAsync(CancellationToken cancellationToken = default);
}
