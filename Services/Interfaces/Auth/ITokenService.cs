using Domain.Models;

namespace Services.Interfaces.Auth
{
    public interface ITokenService
    {
        (string Token, DateTime ExpiresAt) GenerateAccessToken(User user, IEnumerable<string> roles);

        string GenerateRefreshToken();

        Task<RefreshToken> CreateRefreshTokenAsync(Guid userId, CancellationToken cancellationToken = default);

        Task<RefreshToken?> GetActiveRefreshTokenAsync(string token, CancellationToken cancellationToken = default);

        Task RevokeRefreshTokenAsync(string token, CancellationToken cancellationToken = default);

        Task RevokeAllUserRefreshTokensAsync(Guid userId, CancellationToken cancellationToken = default);
    }
}
