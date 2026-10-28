using Domain.Models;
using Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Services.Interfaces.Auth;
using Services.Options;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;

namespace Services.Services.Auth
{
    public class TokenService : ITokenService
    {
        private readonly AppDbContext _context;
        private readonly JwtOptions _options;

        public TokenService(AppDbContext context, IOptions<JwtOptions> options)
        {
            _context = context;
            _options = options.Value;
        }

        public (string Token, DateTime ExpiresAt) GenerateAccessToken(User user, IEnumerable<string> roles)
        {
            var issuedAt = DateTime.UtcNow;
            var expiresAt = issuedAt.AddMinutes(_options.AccessTokenExpirationMinutes);

            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

            var token = new JwtSecurityToken(
                issuer: _options.Issuer,
                audience: _options.Audience,
                claims: claims,
                notBefore: issuedAt,
                expires: expiresAt,
                signingCredentials: new SigningCredentials(_options.GetSigningKey(), SecurityAlgorithms.HmacSha256));

            return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
        }

        public string GenerateRefreshToken()
            => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

        public async Task<RefreshToken> CreateRefreshTokenAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            var refreshToken = new RefreshToken
            {
                UserId = userId,
                Token = GenerateRefreshToken(),
                ExpiredAt = DateTime.UtcNow.AddDays(_options.RefreshTokenExpirationDays)
            };

            _context.RefreshTokens.Add(refreshToken);
            await _context.SaveChangesAsync(cancellationToken);

            return refreshToken;
        }

        public async Task<RefreshToken?> ConsumeRefreshTokenAsync(string token, CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;

            var refreshToken = await _context.RefreshTokens
                .Include(t => t.User)
                .FirstOrDefaultAsync(
                    t => t.Token == token && !t.IsRevoked && t.ExpiredAt > now,
                    cancellationToken);

            if (refreshToken is null)
            {
                return null;
            }

            Revoke(refreshToken);

            try
            {
                // IsRevoked is an optimistic concurrency token, so this update only succeeds
                // while the token is still active in the database.
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                _context.Entry(refreshToken).State = EntityState.Detached;
                return null;
            }

            return refreshToken;
        }

        public async Task<bool> RevokeRefreshTokenAsync(string token, CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;

            // Conditional update keeps revocation atomic: only one concurrent request can affect the row.
            var rowsAffected = await _context.RefreshTokens
                .Where(t => t.Token == token && !t.IsRevoked)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(t => t.IsRevoked, true)
                        .SetProperty(t => t.RevokedAt, now)
                        .SetProperty(t => t.UpdatedAt, now),
                    cancellationToken);

            return rowsAffected > 0;
        }

        public async Task RevokeAllUserRefreshTokensAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;

            // Bulk update avoids loading every active token into memory.
            await _context.RefreshTokens
                .Where(t => t.UserId == userId && !t.IsRevoked)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(t => t.IsRevoked, true)
                        .SetProperty(t => t.RevokedAt, now)
                        .SetProperty(t => t.UpdatedAt, now),
                    cancellationToken);
        }

        private static void Revoke(RefreshToken refreshToken)
        {
            var now = DateTime.UtcNow;

            refreshToken.IsRevoked = true;
            refreshToken.RevokedAt = now;
            refreshToken.UpdatedAt = now;
        }
    }
}
