using Domain.Common;
using Domain.Const;
using Domain.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Services.Accessors.Interfaces;
using Services.Commands.Auth;
using Services.Interfaces.Auth;
using Services.Response.Auth;

namespace Services.Services.Auth
{
    public class AuthService : IAuthService
    {
        private readonly UserManager<User> _userManager;
        private readonly ITokenService _tokenService;
        private readonly IPasswordHasher<User> _passwordHasher;
        private readonly ICancellationTokenAccessor _cancellationTokenAccessor;
        private readonly string _dummyPasswordHash;

        public AuthService(
            UserManager<User> userManager,
            ITokenService tokenService,
            IPasswordHasher<User> passwordHasher,
            ICancellationTokenAccessor cancellationTokenAccessor)
        {
            _userManager = userManager;
            _tokenService = tokenService;
            _passwordHasher = passwordHasher;
            _cancellationTokenAccessor = cancellationTokenAccessor;
            _dummyPasswordHash = _passwordHasher.HashPassword(new User(), "dummy-password-not-used");
        }

        public async Task<Result<AuthResponse>> LoginAsync(
            LoginCommand command,
            CancellationToken cancellationToken = default)
        {
            var token = ResolveCancellationToken(cancellationToken);
            token.ThrowIfCancellationRequested();

            var user = await _userManager.FindByEmailAsync(command.Email);
            token.ThrowIfCancellationRequested();

            if (user is null)
            {
                _passwordHasher.VerifyHashedPassword(new User(), _dummyPasswordHash, command.Password);
                token.ThrowIfCancellationRequested();

                return InvalidCredentials();
            }

            if (await _userManager.IsLockedOutAsync(user))
            {
                token.ThrowIfCancellationRequested();
                return AccountLocked();
            }

            token.ThrowIfCancellationRequested();

            if (!await _userManager.CheckPasswordAsync(user, command.Password))
            {
                token.ThrowIfCancellationRequested();
                await _userManager.AccessFailedAsync(user);
                token.ThrowIfCancellationRequested();

                if (await _userManager.IsLockedOutAsync(user))
                {
                    token.ThrowIfCancellationRequested();
                    return AccountLocked();
                }

                token.ThrowIfCancellationRequested();
                return InvalidCredentials();
            }

            token.ThrowIfCancellationRequested();
            await _userManager.ResetAccessFailedCountAsync(user);
            token.ThrowIfCancellationRequested();

            if (IsBanned(user))
            {
                return AccountBanned();
            }

            if (!user.EmailConfirmed)
            {
                return InvalidCredentials();
            }

            return await CreateAuthResponseAsync(user, token);
        }

        public async Task<Result<AuthResponse>> RefreshAsync(
            string refreshToken,
            CancellationToken cancellationToken = default)
        {
            var token = ResolveCancellationToken(cancellationToken);
            token.ThrowIfCancellationRequested();

            var consumedToken = await _tokenService.ConsumeRefreshTokenAsync(refreshToken, token);
            token.ThrowIfCancellationRequested();

            if (consumedToken is null || consumedToken.User is null)
            {
                return InvalidRefreshToken();
            }

            if (IsBanned(consumedToken.User))
            {
                await _tokenService.RevokeAllUserRefreshTokensAsync(consumedToken.User.Id, token);
                token.ThrowIfCancellationRequested();

                return AccountBanned();
            }

            if (!consumedToken.User.EmailConfirmed)
            {
                return Result<AuthResponse>.Failure(
                    message: "Email address is not confirmed.",
                    errorCode: ErrorCodes.Auth.EmailNotConfirmed,
                    statusCode: StatusCodes.Status403Forbidden);
            }

            return await CreateAuthResponseAsync(consumedToken.User, token);
        }

        public async Task<Result> LogoutAsync(string refreshToken, CancellationToken cancellationToken = default)
        {
            var token = ResolveCancellationToken(cancellationToken);
            token.ThrowIfCancellationRequested();

            await _tokenService.RevokeRefreshTokenAsync(refreshToken, token);
            token.ThrowIfCancellationRequested();

            return Result.Success(
                message: "Logged out successfully.",
                statusCode: StatusCodes.Status200OK);
        }

        private CancellationToken ResolveCancellationToken(CancellationToken cancellationToken)
        {
            var accessorToken = _cancellationTokenAccessor.Token;
            accessorToken.ThrowIfCancellationRequested();

            return cancellationToken.CanBeCanceled
                ? cancellationToken
                : accessorToken;
        }

        private static Result<AuthResponse> InvalidCredentials()
            => Result<AuthResponse>.Failure(
                message: "Invalid email or password.",
                errorCode: ErrorCodes.Auth.InvalidCredentials,
                statusCode: StatusCodes.Status401Unauthorized);

        private static Result<AuthResponse> InvalidRefreshToken()
            => Result<AuthResponse>.Failure(
                message: "Invalid or expired refresh token.",
                errorCode: ErrorCodes.Auth.InvalidRefreshToken,
                statusCode: StatusCodes.Status401Unauthorized);

        private static Result<AuthResponse> AccountLocked()
            => Result<AuthResponse>.Failure(
                message: "Account is temporarily locked due to too many failed login attempts. Please try again later.",
                errorCode: ErrorCodes.Auth.AccountLocked,
                statusCode: StatusCodes.Status423Locked);

        private static Result<AuthResponse> AccountBanned()
            => Result<AuthResponse>.Failure(
                message: "Account is banned.",
                errorCode: ErrorCodes.Auth.AccountBanned,
                statusCode: StatusCodes.Status403Forbidden);

        private static bool IsBanned(User user)
            => user.IsBanned && (user.BannedUntil is null || user.BannedUntil > DateTime.UtcNow);

        private async Task<Result<AuthResponse>> CreateAuthResponseAsync(User user, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var roles = await _userManager.GetRolesAsync(user);

            cancellationToken.ThrowIfCancellationRequested();
            var (accessToken, expiresAt) = _tokenService.GenerateAccessToken(user, roles);

            cancellationToken.ThrowIfCancellationRequested();
            var refreshToken = await _tokenService.CreateRefreshTokenAsync(user.Id, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();
            var response = new AuthResponse
            {
                UserId = user.Id,
                Roles = roles.ToList(),
                AccessToken = accessToken,
                AccessTokenExpiration = expiresAt,
                RefreshToken = refreshToken.Token
            };

            return Result<AuthResponse>.Success(
                message: "Authentication successful.",
                statusCode: StatusCodes.Status200OK,
                data: response);
        }
    }
}
