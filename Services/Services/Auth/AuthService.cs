using Domain.Common;
using Domain.Const;
using Domain.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
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
        private readonly ICancellationTokenAccessor _ctAccessor;
        private readonly string _dummyPasswordHash;
        private readonly ILogger<AuthService> _logger;

        public AuthService(
            UserManager<User> userManager,
            ITokenService tokenService,
            IPasswordHasher<User> passwordHasher,
            ICancellationTokenAccessor ctAccessor,
            ILogger<AuthService> logger)
        {
            _userManager = userManager;
            _tokenService = tokenService;
            _passwordHasher = passwordHasher;
            _ctAccessor = ctAccessor;
            _dummyPasswordHash = _passwordHasher.HashPassword(new User(), "dummy-password-not-used");
            _logger = logger;
        }

        public async Task<Result<AuthResponse>> LoginAsync(
            LoginCommand command,
            CancellationToken cancellationToken = default)
        {
            var token = _ctAccessor.ResolveCancellationToken(cancellationToken);
            token.ThrowIfCancellationRequested();

            var user = await _userManager.FindByEmailAsync(command.Email);

            if (user is null)
            {
                _logger.LogWarning("Login attempt failed for non-existent user with email: {Email}", command.Email);
                _passwordHasher.VerifyHashedPassword(new User(), _dummyPasswordHash, command.Password);
                return InvalidCredentials();
            }

            if (await _userManager.IsLockedOutAsync(user))
            {
                _logger.LogWarning("Login attempt failed for locked out user with email: {Email}", command.Email);
                return AccountLocked();
            }

            if (!await _userManager.CheckPasswordAsync(user, command.Password))
            {
                await _userManager.AccessFailedAsync(user);

                if (await _userManager.IsLockedOutAsync(user))
                {
                    _logger.LogWarning("User with email: {Email} has been locked out due to too many failed login attempts.", command.Email);
                    return AccountLocked();
                }

                _logger.LogWarning("Login attempt failed for user with email: {Email}. Invalid password.", command.Email);
                return InvalidCredentials();
            }

            await _userManager.ResetAccessFailedCountAsync(user);

            if (IsBanned(user))
            {
                _logger.LogWarning("Login attempt failed for banned user with email: {Email}", command.Email);
                return AccountBanned();
            }

            if (!user.EmailConfirmed)
            {
                _logger.LogWarning("Login attempt failed for user with email: {Email}. Email not confirmed.", command.Email);
                return InvalidCredentials();
            }

            _logger.LogInformation("User with email: {Email} logged in successfully.", command.Email);
            return await CreateAuthResponseAsync(user, token);
        }

        public async Task<Result<AuthResponse>> RefreshAsync(
            string refreshToken,
            CancellationToken cancellationToken = default)
        {
            var token = _ctAccessor.ResolveCancellationToken(cancellationToken);
            token.ThrowIfCancellationRequested();

            var consumedToken = await _tokenService.ConsumeRefreshTokenAsync(refreshToken, token);

            if (consumedToken is null || consumedToken.User is null)
            {
                return InvalidRefreshToken();
            }

            if (IsBanned(consumedToken.User))
            {
                _logger.LogWarning("Refresh token attempt failed for banned user with ID: {UserId}", consumedToken.User.Id);
                await _tokenService.RevokeAllUserRefreshTokensAsync(consumedToken.User.Id, token);
                return AccountBanned();
            }

            if (!consumedToken.User.EmailConfirmed)
            {
                _logger.LogWarning("Refresh token attempt failed for user with ID: {UserId}. Email not confirmed.", consumedToken.User.Id);
                return Result<AuthResponse>.Failure(
                    message: "Email address is not confirmed.",
                    errorCode: ErrorCodes.EmailNotConfirmed,
                    statusCode: StatusCodes.Status403Forbidden);
            }

            _logger.LogInformation("Refresh token for user with ID: {UserId} consumed successfully.", consumedToken.User.Id);
            return await CreateAuthResponseAsync(consumedToken.User, token);
        }

        public async Task<Result> LogoutAsync(string refreshToken, CancellationToken cancellationToken = default)
        {
            var token = _ctAccessor.ResolveCancellationToken(cancellationToken);
            token.ThrowIfCancellationRequested();

            await _tokenService.RevokeRefreshTokenAsync(refreshToken, token);

            return Result.Success(
                message: "Logged out successfully.",
                statusCode: StatusCodes.Status200OK);
        }

        private static Result<AuthResponse> InvalidCredentials()
            => Result<AuthResponse>.Failure(
                message: "Invalid email or password.",
                errorCode: ErrorCodes.InvalidCredentials,
                statusCode: StatusCodes.Status401Unauthorized);

        private static Result<AuthResponse> InvalidRefreshToken()
            => Result<AuthResponse>.Failure(
                message: "Invalid or expired refresh token.",
                errorCode: ErrorCodes.InvalidRefreshToken,
                statusCode: StatusCodes.Status401Unauthorized);

        private static Result<AuthResponse> AccountLocked()
            => Result<AuthResponse>.Failure(
                message: "Account is temporarily locked due to too many failed login attempts. Please try again later.",
                errorCode: ErrorCodes.AccountLocked,
                statusCode: StatusCodes.Status423Locked);

        private static Result<AuthResponse> AccountBanned()
            => Result<AuthResponse>.Failure(
                message: "Account is banned.",
                errorCode: ErrorCodes.AccountBanned,
                statusCode: StatusCodes.Status403Forbidden);

        private static bool IsBanned(User user)
            => user.IsBanned && (user.BannedUntil is null || user.BannedUntil > DateTime.UtcNow);

        private async Task<Result<AuthResponse>> CreateAuthResponseAsync(User user, CancellationToken cancellationToken)
        {
            var roles = await _userManager.GetRolesAsync(user);

            var (accessToken, expiresAt) = _tokenService.GenerateAccessToken(user, roles);

            var refreshToken = await _tokenService.CreateRefreshTokenAsync(user.Id, cancellationToken);

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
