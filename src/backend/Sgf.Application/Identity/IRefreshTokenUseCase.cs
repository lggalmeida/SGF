namespace Sgf.Application.Identity;

public interface IRefreshTokenUseCase
{
    Task<RefreshTokenResult> ExecuteAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default);
}
