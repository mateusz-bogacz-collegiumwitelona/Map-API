using Domain.Common;
using Domain.Const;
using Domain.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Services.Interfaces.Auth;
using Services.Response.Auth;

namespace Services.Services.Auth
{
    public class AuthService : IAuthService
    {
        private readonly UserManager<User> _userManager;
        private readonly ITokenService _tokenService;

        public AuthService(UserManager<User> userManager, ITokenService tokenService)
        {
            _userManager = userManager;
            _tokenService = tokenService;
        }

        public async Task<Result<AuthResponse>> LoginAsync(
            string email,
            string password,
            CancellationToken cancellationToken = default)
        {
            var user = await _userManager.FindByEmailAsync(email);

            if (user is null || !await _userManager.CheckPasswordAsync(user, password))
            {
                return Result<AuthResponse>.Failure(
                    message: "Invalid email or password.",
                    errorCode: ErrorCodes.InvalidCredentials,
                    statusCode: StatusCodes.Status401Unauthorized);
            }

            if (!user.EmailConfirmed)
            {
                return Result<AuthResponse>.Failure(
                    message: "Email address is not confirmed.",
                    errorCode: ErrorCodes.EmailNotConfirmed,
                    statusCode: StatusCodes.Status403Forbidden);
            }

            return await CreateAuthResponseAsync(user, cancellationToken);
        }

        public async Task<Result<AuthResponse>> RefreshAsync(
            string refreshToken,
            CancellationToken cancellationToken = default)
        {
            var storedToken = await _tokenService.GetActiveRefreshTokenAsync(refreshToken, cancellationToken);

            if (storedToken is null)
            {
                return Result<AuthResponse>.Failure(
                    message: "Invalid or expired refresh token.",
                    errorCode: ErrorCodes.InvalidRefreshToken,
                    statusCode: StatusCodes.Status401Unauthorized);
            }

            if (!storedToken.User.EmailConfirmed)
            {
                await _tokenService.RevokeRefreshTokenAsync(storedToken.Token, cancellationToken);

                return Result<AuthResponse>.Failure(
                    message: "Email address is not confirmed.",
                    errorCode: ErrorCodes.EmailNotConfirmed,
                    statusCode: StatusCodes.Status403Forbidden);
            }

            await _tokenService.RevokeRefreshTokenAsync(storedToken.Token, cancellationToken);

            return await CreateAuthResponseAsync(storedToken.User, cancellationToken);
        }

        public async Task<Result> LogoutAsync(string refreshToken, CancellationToken cancellationToken = default)
        {
            await _tokenService.RevokeRefreshTokenAsync(refreshToken, cancellationToken);

            return Result.Success(
                message: "Logged out successfully.",
                statusCode: StatusCodes.Status200OK);
        }

        private async Task<Result<AuthResponse>> CreateAuthResponseAsync(User user, CancellationToken cancellationToken)
        {
            var roles = await _userManager.GetRolesAsync(user);
            var (accessToken, expiresAt) = _tokenService.GenerateAccessToken(user, roles);
            var refreshToken = await _tokenService.CreateRefreshTokenAsync(user.Id, cancellationToken);

            var response = new AuthResponse(
                UserId: user.Id,
                Roles: roles.ToList(),
                AccessToken: accessToken,
                AccessTokenExpiration: expiresAt,
                RefreshToken: refreshToken.Token);

            return Result<AuthResponse>.Success(
                message: "Authentication successful.",
                statusCode: StatusCodes.Status200OK,
                data: response);
        }
    }
}
