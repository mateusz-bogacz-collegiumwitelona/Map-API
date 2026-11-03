using Domain.Common;
using Domain.Const;
using Domain.Enum;
using Domain.Models;
using Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using Services.Accessors.Interfaces;
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

        private CancellationToken _ct => _ctAccessor.Token;

        public AccountServices(
            AppDbContext context,
            ITokenService tokenService,
            IEmailService emailService,
            ILookupNormalizer normalizer,
            ILogger<AccountServices> logger,
            ICancellationTokenAccessor ctAccessor)
        {
            _context = context;
            _tokenService = tokenService;
            _emailService = emailService;
            _normalizer = normalizer;
            _logger = logger;
            _ctAccessor = ctAccessor;
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
                    errorCode: ErrorCodes.User.EmailUnchanged,
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
                    errorCode: ErrorCodes.Verification.CodeExpired,
                    statusCode: StatusCodes.Status400BadRequest);
            }

            if (verificationCode.AttemptsCount >= VerificationConst.MaxAttempts)
            {
                return Result.Failure(
                    message: "Too many incorrect attempts. Please request a new verification code.",
                    errorCode: ErrorCodes.Verification.AttemptsExceeded,
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

        private static bool IsUniqueViolation(DbUpdateException exception)
            => exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

        private static bool CodesMatch(string expected, string actual)
            => CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(actual));

        private static Result UserNotFound(Guid userId)
            => Result.Failure(
                message: $"User with ID '{userId}' was not found.",
                errorCode: ErrorCodes.Common.NotFound,
                statusCode: StatusCodes.Status404NotFound);

        private static Result EmailTaken()
            => Result.Failure(
                message: "This email address is already in use.",
                errorCode: ErrorCodes.User.EmailTaken,
                statusCode: StatusCodes.Status409Conflict);

        private static Result CodeInvalid()
            => Result.Failure(
                message: "The verification code is invalid.",
                errorCode: ErrorCodes.Verification.CodeInvalid,
                statusCode: StatusCodes.Status400BadRequest);
    }
}
