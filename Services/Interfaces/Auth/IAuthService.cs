using Domain.Common;
using Services.Commands.Auth;
using Services.Response.Auth;

namespace Services.Interfaces.Auth
{
    public interface IAuthService
    {
        Task<Result<AuthResponse>> LoginAsync(LoginCommand command, CancellationToken cancellationToken = default);

        Task<Result<AuthResponse>> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default);

        Task<Result> LogoutAsync(string refreshToken, CancellationToken cancellationToken = default);
    }
}
