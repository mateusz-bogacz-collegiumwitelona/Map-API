using Domain.Common;
using Domain.Const;
using Domain.Models;
using Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Services.Accessors.Interfaces;
using Services.Command.Auth;
using Services.Commands.Auth;
using Services.Interfaces.Auth;
using Services.Response.Auth;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Domain.Exceptions;

namespace Services.Services.Auth
{
    public class AuthService : IAuthService
    {
        private readonly UserManager<User> _userManager;
        private readonly ITokenService _tokenService;
        private readonly IPasswordHasher<User> _passwordHasher;
        private readonly ICancellationTokenAccessor _ctAccessor;
        private readonly string _dummyPasswordHash;
        private readonly AppDbContext _dbContext;
        private readonly ILogger<AuthService> _logger;

        public AuthService(
            UserManager<User> userManager,
            ITokenService tokenService,
            IPasswordHasher<User> passwordHasher,
            ICancellationTokenAccessor ctAccessor,
            AppDbContext dbContext,
            ILogger<AuthService> logger)
        {
            _userManager = userManager;
            _tokenService = tokenService;
            _passwordHasher = passwordHasher;
            _ctAccessor = ctAccessor;
            _dummyPasswordHash = _passwordHasher.HashPassword(new User(), "dummy-password-not-used");
            _dbContext = dbContext;
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
                _logger.LogWarning("Refresh token attempt failed. Invalid or expired refresh token: {RefreshToken}", refreshToken);
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

            _logger.LogInformation("Refresh token revoked successfully: {RefreshToken}", refreshToken);
            return Result.Success(
                message: "Logged out successfully.",
                statusCode: StatusCodes.Status200OK);
        }

        public async Task<Result<string>> RegisterAsync(RegisterCommand command, CancellationToken ct = default)
        {
            var token = _ctAccessor.ResolveCancellationToken(ct);
            token.ThrowIfCancellationRequested();

            var existingUser = await _userManager.FindByEmailAsync(command.Email) is not null;

            if (existingUser)
            {
                _logger.LogWarning("Registration attempt for already registered email: {Email}", command.Email);
                return Result<string>.Success(
                    message: "If the email is valid, a verification code has been generated.",
                    statusCode: StatusCodes.Status200OK,
                    data: string.Empty);
            }

            await using var transaction = await _dbContext.Database.BeginTransactionAsync(token);

            var newUser = new User
            {
                UserName = command.Email,
                Email = command.Email,
                FirstName = command.FirstName,
                LastName = command.LastName,
                Bio = string.IsNullOrWhiteSpace(command.Bio) ? null : command.Bio,
                EmailConfirmed = false
            };

            var createUser = await _userManager.CreateAsync(newUser, command.Password);
            if (!createUser.Succeeded)
            {
                var errors = string.Join(", ", createUser.Errors.Select(e => e.Description));
                _logger.LogError("User creation failed for email: {Email}. Errors: {Errors}", command.Email, errors);
                throw new InternalServerException($"Failed to create user: {errors}");
            }

            var roleAssign = await _userManager.AddToRoleAsync(newUser, "User");

            if (!roleAssign.Succeeded)
            {
                var errors = string.Join(", ", roleAssign.Errors.Select(e => e.Description));   
                _logger.LogCritical("Default role 'User' could not be assigned. Errors: {Errors}", errors);
                throw new InternalServerException($"Failed to assign default role: {errors}");
            }

            var rawCode = RandomNumberGenerator.GetInt32(100_000, 1_000_000).ToString("D6");

            var codeEntity = new EmailVerificationCode
            {
                UserId = newUser.Id,
                Code = rawCode,
                ExpiredAt = DateTime.UtcNow.AddHours(4),
                AttemptsCount = 0,
                IsUsed = false
            };

            await _dbContext.EmailVerificationCodes.AddAsync(codeEntity, token);
            await _dbContext.SaveChangesAsync(token);

            await transaction.CommitAsync(token);

            return Result<string>.Success(
                message: "User registered successfully.",
                statusCode: StatusCodes.Status201Created,
                data: rawCode);
        }

        public async Task<Result> ConfirmEmailAsync(ConfirmEmailCommand command, CancellationToken ct = default)
        {
            var token = _ctAccessor.ResolveCancellationToken(ct);
            token.ThrowIfCancellationRequested();

            var user = await _userManager.FindByEmailAsync(command.Email);

            if (user == null)
            {
                _logger.LogWarning("Email confirmation attempt failed for non-existent user with email: {Email}", command.Email);
                return Result.Failure(
                    message: "Invalid email or token.",
                    errorCode: ErrorCodes.InvalidCredentials,
                    statusCode: StatusCodes.Status400BadRequest);
            }

            if (user.EmailConfirmed)
            {
                _logger.LogInformation("Email confirmation attempt for already confirmed email: {Email}", command.Email);
                return Result.Success(
                    message: "Email is already confirmed.",
                    statusCode: StatusCodes.Status200OK
                    );
            }

            var verificationCode = await _dbContext.EmailVerificationCodes
                .Where(c => c.UserId == user.Id && !c.IsUsed)
                .OrderByDescending(c => c.CreatedAt)
                .FirstOrDefaultAsync(token);

            if (verificationCode == null)
            {
                _logger.LogWarning("Email confirmation attempt failed for user with email: {Email}. No verification code found.", command.Email);
                return Result.Failure(
                    message: "Invalid email or token.",
                    errorCode: ErrorCodes.InvalidCredentials,
                    statusCode: StatusCodes.Status400BadRequest);
            }


            if (verificationCode.AttemptsCount >= 5)
            {
                verificationCode.IsUsed = true;
                await _dbContext.SaveChangesAsync(token);

                _logger.LogWarning("Email confirmation attempt failed for user with email: {Email}. Maximum attempts reached.", command.Email);
                return Result.Failure(
                    message: "Maximum verification attempts reached. Please request a new verification code.",
                    errorCode: ErrorCodes.AccountLocked,
                    statusCode: StatusCodes.Status429TooManyRequests);
            }

            if (verificationCode.ExpiredAt < DateTime.UtcNow)
            {
                verificationCode.IsUsed = true;
                await _dbContext.SaveChangesAsync(token);

                _logger.LogWarning("Email confirmation attempt failed for user with email: {Email}. Verification code expired.", command.Email);
                return Result.Failure(
                    message: "Verification code has expired. Please request a new verification code.",
                    errorCode: ErrorCodes.InvalidCredentials,
                    statusCode: StatusCodes.Status400BadRequest);
            }

            if (!string.Equals(verificationCode.Code, command.Code, StringComparison.Ordinal))
            {
                verificationCode.AttemptsCount++;
               
                if (verificationCode.AttemptsCount >= 5)
                {
                    verificationCode.IsUsed = true;
                }

                await _dbContext.SaveChangesAsync(token);

                _logger.LogWarning("Email confirmation attempt failed for user with email: {Email}. Invalid verification code.", command.Email);
                return Result.Failure(
                    message: "Invalid verification code.",
                    errorCode: ErrorCodes.InvalidCredentials,
                    statusCode: StatusCodes.Status400BadRequest);
            }

            await using var transaction = await _dbContext.Database.BeginTransactionAsync(token);

            verificationCode.IsUsed = true;
            user.EmailConfirmed = true;

            var updateResult = await _userManager.UpdateAsync(user);

            if (!updateResult.Succeeded)
            {
                var errors = string.Join(", ", updateResult.Errors.Select(e => e.Description));            
                _logger.LogError("Failed to update email confirmation status for user ID: {UserId}. Errors: {Errors}", user.Id, errors);
                throw new InternalServerException($"Failed to confirm user email due to persistence error: {errors}");
            }

            await _dbContext.SaveChangesAsync(token);
            await transaction.CommitAsync(token);

            _logger.LogInformation("Email confirmed successfully for user with email: {Email}", command.Email);
            return Result.Success(
                message: "Email confirmed successfully.",
                statusCode: StatusCodes.Status200OK
                );

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
