using Domain.Common;
using Domain.Const;
using Domain.Enum;
using Domain.Exceptions;
using Domain.Models;
using Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using Services.Accessors.Interfaces;
using Services.Command.Auth;
using Services.Command.User;
using Services.Interfaces;
using Services.Interfaces.Auth;
using System.Security.Cryptography;
using System.Text;

namespace Services.Services
{
    public class AccountServices : IAccountServices
    {
        private const string DeletedEmailDomain = "deleted.invalid";

        private readonly AppDbContext _context;
        private readonly ITokenService _tokenService;
        private readonly IEmailService _emailService;
        private readonly ILookupNormalizer _normalizer;
        private readonly ILogger<AccountServices> _logger;
        private readonly ICancellationTokenAccessor _ctAccessor;
        private readonly UserManager<User> _userManager;

        private CancellationToken _ct => _ctAccessor.Token;

        public AccountServices(
            AppDbContext context,
            ITokenService tokenService,
            IEmailService emailService,
            ILookupNormalizer normalizer,
            ILogger<AccountServices> logger,
            ICancellationTokenAccessor ctAccessor,
            UserManager<User> userManager)
        {
            _context = context;
            _tokenService = tokenService;
            _emailService = emailService;
            _normalizer = normalizer;
            _logger = logger;
            _ctAccessor = ctAccessor;
            _userManager = userManager;
        }

        public async Task<Result> RequestEmailChangeAsync(Guid userId, ChangeEmailCommand command)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId, _ct);

            if (user == null)
            {
                return UserNotFound(userId);
            }

            var newEmail = command.NewEmail;
            var normalizedEmail = _normalizer.NormalizeEmail(newEmail);

            if (normalizedEmail == user.NormalizedEmail)
            {
                return Result.Failure(
                    message: "The new email address must be different from the current one.",
                    errorCode: ErrorCodes.EmailUnchanged,
                    statusCode: StatusCodes.Status400BadRequest);
            }

            if (await IsEmailTakenAsync(normalizedEmail, userId))
            {
                return EmailTaken();
            }

            var verificationCode = new EmailVerificationCode
            {
                UserId = userId,
                Purpose = VerificationPurposeEnum.EmailChange,
                NewEmail = newEmail,
                Code = RandomNumberGenerator.GetString("0123456789", VerificationConst.CodeLength),
                ExpiredAt = DateTime.UtcNow.AddMinutes(VerificationConst.CodeExpirationMinutes)
            };

            await PendingEmailChangeCodes(userId).ExecuteDeleteAsync(_ct);

            _context.EmailVerificationCodes.Add(verificationCode);
            await _context.SaveChangesAsync(_ct);

            await _emailService.SendEmailChangeCodeAsync(newEmail, user.FirstName, verificationCode.Code, VerificationConst.CodeExpirationMinutes, _ct);

            _logger.LogInformation("Email change code sent for user ID: {UserId}", userId);

            return Result.Success(
                message: "A verification code has been sent to the new email address.",
                statusCode: StatusCodes.Status200OK);
        }

        public async Task<Result> ConfirmEmailChangeAsync(Guid userId, ConfirmEmailChangeCommand command)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId, _ct);

            if (user == null)
            {
                return UserNotFound(userId);
            }

            var verificationCode = await PendingEmailChangeCodes(userId)
                .AsNoTracking()
                .OrderByDescending(c => c.CreatedAt)
                .FirstOrDefaultAsync(_ct);

            if (verificationCode == null || string.IsNullOrWhiteSpace(verificationCode.NewEmail))
            {
                return CodeInvalid();
            }

            if (verificationCode.ExpiredAt <= DateTime.UtcNow)
            {
                return Result.Failure(
                    message: "The verification code has expired. Please request a new one.",
                    errorCode: ErrorCodes.CodeExpired,
                    statusCode: StatusCodes.Status400BadRequest);
            }

            if (verificationCode.AttemptsCount >= VerificationConst.MaxAttempts)
            {
                return Result.Failure(
                    message: "Too many incorrect attempts. Please request a new verification code.",
                    errorCode: ErrorCodes.AttemptsExceeded,
                    statusCode: StatusCodes.Status400BadRequest);
            }

            if (!CodesMatch(verificationCode.Code, command.Code))
            {
                await _context.EmailVerificationCodes
                    .Where(c => c.Id == verificationCode.Id && !c.IsUsed && c.AttemptsCount < VerificationConst.MaxAttempts)
                    .ExecuteUpdateAsync(
                        setters => setters
                            .SetProperty(c => c.AttemptsCount, c => c.AttemptsCount + 1)
                            .SetProperty(c => c.UpdatedAt, DateTime.UtcNow),
                        _ct);

                return CodeInvalid();
            }

            var newEmail = verificationCode.NewEmail;
            var normalizedEmail = _normalizer.NormalizeEmail(newEmail);

            if (await IsEmailTakenAsync(normalizedEmail, userId))
            {
                return EmailTaken();
            }

            await using var transaction = await _context.Database.BeginTransactionAsync(_ct);

            var codeConsumed = await _context.EmailVerificationCodes
                .Where(c => c.Id == verificationCode.Id && !c.IsUsed)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(c => c.IsUsed, true)
                        .SetProperty(c => c.UpdatedAt, DateTime.UtcNow),
                    _ct);

            if (codeConsumed == 0)
            {
                return CodeInvalid();
            }

            var oldEmail = user.Email;

            if (string.Equals(user.UserName, oldEmail, StringComparison.OrdinalIgnoreCase))
            {
                user.UserName = newEmail;
                user.NormalizedUserName = _normalizer.NormalizeName(newEmail);
            }

            user.Email = newEmail;
            user.NormalizedEmail = normalizedEmail;
            user.EmailConfirmed = true;
            RefreshStamps(user);

            try
            {
                await _context.SaveChangesAsync(_ct);
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex))
            {
                return EmailTaken();
            }

            await transaction.CommitAsync(_ct);

            _logger.LogInformation("Email address changed for user ID: {UserId}", userId);

            await NotifyEmailChangedAsync(oldEmail, user.FirstName, newEmail, userId);

            return Result.Success(
                message: "Email address has been changed successfully.",
                statusCode: StatusCodes.Status200OK);
        }

        public async Task<Result> DeleteAccountAsync(Guid userId)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId, _ct);

            if (user == null)
            {
                return UserNotFound(userId);
            }

            await using var transaction = await _context.Database.BeginTransactionAsync(_ct);

            await _tokenService.RevokeAllUserRefreshTokensAsync(userId, _ct);

            await _context.EmailVerificationCodes
                .Where(c => c.UserId == userId)
                .ExecuteDeleteAsync(_ct);

            Anonymize(user);
            _context.Users.Remove(user);

            await _context.SaveChangesAsync(_ct);
            await transaction.CommitAsync(_ct);

            _logger.LogInformation("Account soft-deleted and anonymized for user ID: {UserId}", userId);

            return Result.Success(
                message: "Account has been deleted successfully.",
                statusCode: StatusCodes.Status200OK);
        }

        private IQueryable<EmailVerificationCode> PendingEmailChangeCodes(Guid userId)
            => _context.EmailVerificationCodes
                .Where(c => c.UserId == userId && c.Purpose == VerificationPurposeEnum.EmailChange && !c.IsUsed);

        private Task<bool> IsEmailTakenAsync(string normalizedEmail, Guid currentUserId)
            => _context.Users
                .IgnoreQueryFilters()
                .AnyAsync(u => u.Id != currentUserId
                    && (u.NormalizedEmail == normalizedEmail || u.NormalizedUserName == normalizedEmail), _ct);

        private void Anonymize(User user)
        {
            var anonymousEmail = $"deleted-{user.Id:N}@{DeletedEmailDomain}";

            user.Email = anonymousEmail;
            user.NormalizedEmail = _normalizer.NormalizeEmail(anonymousEmail);
            user.UserName = anonymousEmail;
            user.NormalizedUserName = _normalizer.NormalizeName(anonymousEmail);
            user.EmailConfirmed = false;
            user.PasswordHash = null;
            user.PhoneNumber = null;
            user.PhoneNumberConfirmed = false;
            user.FirstName = "Deleted";
            user.LastName = "User";
            user.Bio = null;
            user.IsPublic = false;
            RefreshStamps(user);
        }

        private static void RefreshStamps(User user)
        {
            user.SecurityStamp = Guid.NewGuid().ToString("N").ToUpperInvariant();
            user.ConcurrencyStamp = Guid.NewGuid().ToString();
        }

        private async Task NotifyEmailChangedAsync(string? oldEmail, string firstName, string newEmail, Guid userId)
        {
            if (string.IsNullOrWhiteSpace(oldEmail))
            {
                return;
            }

            try
            {
                await _emailService.SendEmailChangedNotificationAsync(oldEmail, firstName, newEmail, _ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Failed to send email change notification for user ID: {UserId}", userId);
            }
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

            await using var transaction = await _context.Database.BeginTransactionAsync(token);

            var newUser = new User
            {
                UserName = command.Email,
                Email = command.Email,
                FirstName = command.FirstName,
                LastName = command.LastName,
                Bio = string.IsNullOrWhiteSpace(command.Bio) ? null : command.Bio,
                EmailConfirmed = false,
                IsPublic = command.IsPublic
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

            await _context.EmailVerificationCodes.AddAsync(codeEntity, token);
            await _context.SaveChangesAsync(token);

            await transaction.CommitAsync(token);

            return Result<string>.Success(
                message: "User registered successfully.",
                statusCode: StatusCodes.Status201Created,
                data: rawCode);
        }

        public async Task<Result> ConfirmRegisterAsync(ConfirmEmailCommand command, CancellationToken ct = default)
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

            var verificationCode = await _context.EmailVerificationCodes
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
                await _context.SaveChangesAsync(token);

                _logger.LogWarning("Email confirmation attempt failed for user with email: {Email}. Maximum attempts reached.", command.Email);
                return Result.Failure(
                    message: "Maximum verification attempts reached. Please request a new verification code.",
                    errorCode: ErrorCodes.AccountLocked,
                    statusCode: StatusCodes.Status429TooManyRequests);
            }

            if (verificationCode.ExpiredAt < DateTime.UtcNow)
            {
                verificationCode.IsUsed = true;
                await _context.SaveChangesAsync(token);

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

                await _context.SaveChangesAsync(token);

                _logger.LogWarning("Email confirmation attempt failed for user with email: {Email}. Invalid verification code.", command.Email);
                return Result.Failure(
                    message: "Invalid verification code.",
                    errorCode: ErrorCodes.InvalidCredentials,
                    statusCode: StatusCodes.Status400BadRequest);
            }

            await using var transaction = await _context.Database.BeginTransactionAsync(token);

            verificationCode.IsUsed = true;
            user.EmailConfirmed = true;

            var updateResult = await _userManager.UpdateAsync(user);

            if (!updateResult.Succeeded)
            {
                var errors = string.Join(", ", updateResult.Errors.Select(e => e.Description));
                _logger.LogError("Failed to update email confirmation status for user ID: {UserId}. Errors: {Errors}", user.Id, errors);
                throw new InternalServerException($"Failed to confirm user email due to persistence error: {errors}");
            }

            await _context.SaveChangesAsync(token);
            await transaction.CommitAsync(token);

            _logger.LogInformation("Email confirmed successfully for user with email: {Email}", command.Email);
            return Result.Success(
                message: "Email confirmed successfully.",
                statusCode: StatusCodes.Status200OK
                );

        }


        private static bool IsUniqueViolation(DbUpdateException exception)
            => exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

        private static bool CodesMatch(string expected, string actual)
            => CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(actual));

        private static Result UserNotFound(Guid userId)
            => Result.Failure(
                message: $"User with ID '{userId}' was not found.",
                errorCode: ErrorCodes.NotFound,
                statusCode: StatusCodes.Status404NotFound);

        private static Result EmailTaken()
            => Result.Failure(
                message: "This email address is already in use.",
                errorCode: ErrorCodes.EmailTaken,
                statusCode: StatusCodes.Status409Conflict);

        private static Result CodeInvalid()
            => Result.Failure(
                message: "The verification code is invalid.",
                errorCode: ErrorCodes.CodeInvalid,
                statusCode: StatusCodes.Status400BadRequest);
    }
}
