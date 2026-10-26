using Domain.Common;
using Services.Response.Auth;

namespace Services.Interfaces.Auth
{
    public interface IAuthService
    {
        Task<Result<AuthResponse>> LoginAsync(string email, string password, CancellationToken cancellationToken = default);

        Task<Result<AuthResponse>> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default);

        Task<Result> LogoutAsync(string refreshToken, CancellationToken cancellationToken = default);
    }
}
