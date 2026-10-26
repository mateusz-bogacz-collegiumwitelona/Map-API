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
using System.Text;

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
                signingCredentials: new SigningCredentials(CreateSigningKey(), SecurityAlgorithms.HmacSha256));

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

        public async Task<RefreshToken?> GetActiveRefreshTokenAsync(string token, CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;

            return await _context.RefreshTokens
                .Include(t => t.User)
                .FirstOrDefaultAsync(
                    t => t.Token == token && !t.IsRevoked && t.ExpiredAt > now,
                    cancellationToken);
        }

        public async Task RevokeRefreshTokenAsync(string token, CancellationToken cancellationToken = default)
        {
            var refreshToken = await _context.RefreshTokens
                .FirstOrDefaultAsync(t => t.Token == token, cancellationToken);

            if (refreshToken is null || refreshToken.IsRevoked)
            {
                return;
            }

            Revoke(refreshToken);
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task RevokeAllUserRefreshTokensAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            var refreshTokens = await _context.RefreshTokens
                .Where(t => t.UserId == userId && !t.IsRevoked)
                .ToListAsync(cancellationToken);

            if (refreshTokens.Count == 0)
            {
                return;
            }

            foreach (var refreshToken in refreshTokens)
            {
                Revoke(refreshToken);
            }

            await _context.SaveChangesAsync(cancellationToken);
        }

        private SymmetricSecurityKey CreateSigningKey()
        {
            if (string.IsNullOrWhiteSpace(_options.Key) || Encoding.UTF8.GetByteCount(_options.Key) < 32)
            {
                throw new InvalidOperationException(
                    "JWT signing key is missing or too short. Configure JWT:KEY with at least 32 characters.");
            }

            return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key));
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
