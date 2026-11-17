using Domain.Common;
using Domain.Const;
using Domain.Enum;
using Domain.Exceptions;
using Domain.Models;
using Domain.State;
using Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Services.Accessors.Interfaces;
using Services.Command.Ban;
using Services.Command.List;
using Services.Helpers;
using Services.Interfaces;
using Services.Interfaces.Auth;
using Services.QueryExtension;
using Services.Response.Ban;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

namespace Services.Services
{
    public class BanServices : IBanServices
    {
        private readonly AppDbContext _context;
        private readonly ILogger<BanServices> _logger;
        private readonly ICancellationTokenAccessor _ctAccessor;
        private readonly UserManager<User> _userManager;
        private readonly IEmailService _emailService;
        private readonly ITokenService _tokenService;

        private CancellationToken _ct => _ctAccessor.Token;

        public BanServices(
            AppDbContext context,
            ILogger<BanServices> logger,
            ICancellationTokenAccessor ctAccessor,
            UserManager<User> userManager,
            IEmailService emailService,
            ITokenService tokenService)
        {
            _context = context;
            _logger = logger;
            _ctAccessor = ctAccessor;
            _userManager = userManager;
            _emailService = emailService;
            _tokenService = tokenService;
        }

        public async Task<Result> ReportUserAsync(ReportUserCommand command, Guid currentUserId)
        {
            if (command.UserId == currentUserId)
            {
                _logger.LogInformation("User {UserId} attempted to report themselves.", command.UserId);

                return Result.Failure(
                    message: "You cannot report yourself.",
                    errorCode: ErrorCodes.InvalidOperation,
                    statusCode: StatusCodes.Status400BadRequest);
            }

            var reportedUser = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == command.UserId, _ct);

            if (reportedUser == null)
            {
                _logger.LogWarning("User {UserId} attempted to report a non-existent user {TargetUserId}.", currentUserId, command.UserId);
                return Result.Failure(
                    message: "The user you are trying to report does not exist.",
                    errorCode: ErrorCodes.NotFound,
                    statusCode: StatusCodes.Status404NotFound);
            }

            if (await IsAdminAsync(reportedUser))
            {
                _logger.LogWarning("User {UserId} attempted to report an admin {TargetUserId}.", currentUserId, command.UserId);
                return Result.Failure(
                    message: "You cannot report an administrator.",
                    errorCode: ErrorCodes.InvalidOperation,
                    statusCode: StatusCodes.Status400BadRequest);
            }

            var report = new UserReport
            {
                ReportedUserId = command.UserId,
                ReporterUserId = currentUserId,
                Reason = command.Reason
            };

            _context.UserReports.Add(report);
            await _context.SaveChangesAsync(_ct);

            _logger.LogInformation("User {ReporterId} reported user {ReportedId} with reason: {Reason}", currentUserId, command.UserId, command.Reason);
            return Result.Success(
                message: "User reported successfully.",
                statusCode: StatusCodes.Status201Created
                );
        }

        public async Task<Result<PagedResult<ReportListResponse>>> GetReportListAsync(ReportListCommand command)
            => await _context.UserReports
                .AsNoTracking()
                .ApplyFilter(command.IsShowAll)
                .ApplySorting(command.SortBy, command.SortDescending)
                .ApplySearch(command.SearchTerm)
                .Select(ur => new ReportListResponse
                {
                    Id = ur.Id,
                    ReportedUserId = ur.ReportedUserId,
                    FirstName = ur.ReportedUser.FirstName,
                    LastName = ur.ReportedUser.LastName,
                    Reason = ur.Reason.Length > 50 ? ur.Reason.Substring(0, 50) + "..." : ur.Reason,
                    Status = ur.Status,
                    CreatedAt = ur.CreatedAt,
                    ResolvedAt = ur.ResolvedAt
                })
            .ToPagedResultAsync(command.PageNumber, command.PageSize, _logger, "Reports", _ct);
        public async Task<Result<ReportDetailResponse>> GetReportDetailAsync(Guid id)
        {
            var report = await _context.UserReports
                .AsNoTracking()
                .Where(ur => ur.Id == id)
                .Select(ur => new ReportDetailResponse
                {
                    Id = ur.Id,

                    ReportedFirstName = ur.ReportedUser.FirstName,
                    ReportedLastName = ur.ReportedUser.LastName,

                    ReporterFirstName = ur.ReporterUser.FirstName,
                    ReporterLastName = ur.ReporterUser.LastName,

                    Reason = ur.Reason,
                    Status = ur.Status,

                    ResolvedByAdminFirstName = ur.ResolvedByAdmin != null
                        ? ur.ResolvedByAdmin.FirstName
                        : null,

                    ResolvedByAdminLastName = ur.ResolvedByAdmin != null
                        ? ur.ResolvedByAdmin.LastName
                        : null,

                    ResolvedAt = ur.ResolvedAt,
                    CreatedAt = ur.CreatedAt
                })
                .FirstOrDefaultAsync(_ct);

            if (report == null)
            {
                _logger.LogWarning("Report with ID {ReportId} not found.", id);

                return Result<ReportDetailResponse>.Failure(
                    message: "Report not found.",
                    errorCode: ErrorCodes.ReportNotFound,
                    statusCode: StatusCodes.Status404NotFound);
            }

            _logger.LogInformation("Retrieved details for report ID: {ReportId}", id);
            return Result<ReportDetailResponse>.Success(
                message: "Report details retrieved successfully.",
                data: report,
                statusCode: StatusCodes.Status200OK);
        }

        public async Task<Result> ResolveReportAsync(ResolveReportCommand command, Guid adminId)
        {
            var report = await _context.UserReports
                .Include(ur => ur.ReportedUser)
                .FirstOrDefaultAsync(ur => ur.Id == command.Id, _ct);

            if (report == null)
            {
                _logger.LogWarning("Admin with id {AdminId} tried to resolve report with id {ReportId} which doesn't exist.",
                    adminId, command.Id);
                return Result.Failure(
                    message: "Report does not exist.",
                    statusCode: StatusCodes.Status404NotFound,
                    errorCode: ErrorCodes.ReportNotFound);
            }

            var stateMachine = new UserReportStateMachine(report);

            if (!stateMachine.CanReject() || !stateMachine.CanApprove())
            {
                _logger.LogWarning("Admin with id {AdminId} tried to resolve report with id {ReportId} which is already resolved.",
                    adminId, command.Id);

                return Result.Failure(
                    message: "Report has already been resolved.",
                    statusCode: StatusCodes.Status400BadRequest,
                    errorCode: ErrorCodes.ReportAlreadyResolved);
            }

            if (!command.IsBanned)
            {
                stateMachine.Reject(adminId);
                await _context.SaveChangesAsync(_ct);
            }
            else
            {
                if (report.ReportedUserId == adminId)
                {
                    _logger.LogWarning("Admin with id {AdminId} attempted to ban themselves via report {ReportId}.",
                        adminId, report.Id);
                    return Result.Failure(
                        message: "You cannot ban yourself.",
                        statusCode: StatusCodes.Status400BadRequest,
                        errorCode: ErrorCodes.InvalidOperation);
                }

                if (await IsAdminAsync(report.ReportedUser))
                {
                    _logger.LogWarning("Admin with id {AdminId} attempted to ban admin {UserId} via report {ReportId}.",
                        adminId, report.ReportedUserId, report.Id);
                    return Result.Failure(
                        message: "You cannot ban an administrator.",
                        statusCode: StatusCodes.Status400BadRequest,
                        errorCode: ErrorCodes.InvalidOperation);
                }

                if (report.ReportedUser.HasActiveBan())
                {
                    _logger.LogWarning("Admin with id {AdminId} tried to ban user with id {UserId} who is already banned.",
                        adminId, report.ReportedUserId);
                    return Result.Failure(
                        message: "Reported user is already banned.",
                        statusCode: StatusCodes.Status400BadRequest,
                        errorCode: ErrorCodes.UserAlreadyBanned);
                }

                stateMachine.Approve(adminId);
                await ApplyBanAsync(report.ReportedUser, command.Duration, command.Reason!);
                await ApprovePendingReportsForUserAsync(report.ReportedUserId, adminId, excludeReportId: report.Id);

                await _context.SaveChangesAsync(_ct);
                await SendBanEmailSafeAsync(report.ReportedUser, command.Reason!);
            }

            return Result.Success(
                message: "Report resolved successfully.",
                statusCode: StatusCodes.Status200OK);
        }

        public async Task<Result> UnbanUserAsync(Guid userId, Guid adminId)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());

            if (user == null)
            {
                _logger.LogWarning("Admin with id {AdminId} tried to unban non-existent user with id {UserId}.",
                    adminId, userId);
                return Result.Failure(
                    message: "User does not exist.",
                    statusCode: StatusCodes.Status404NotFound,
                    errorCode: ErrorCodes.NotFound);
            }


            if (!user.HasActiveBan())
            {
                _logger.LogWarning("Admin with id {AdminId} tried to unban user with id {UserId} who is not currently banned.",
                    adminId, userId);

                return Result.Failure(
                    message: "User is not currently banned.",
                    statusCode: StatusCodes.Status400BadRequest,
                    errorCode: ErrorCodes.UserNotBanned);
            }

            user.Unban();
            await _context.SaveChangesAsync(_ct);

            _logger.LogInformation("Admin with id {AdminId} unbanned user with id {UserId}.", adminId, userId);
            return Result.Success(
                message: "User unbanned successfully.",
                statusCode: StatusCodes.Status200OK);
        }

        public async Task<Result> BanUserAsync(BanUserCommand command, Guid adminId)
        {
            if (command.UserId == adminId)
            {
                _logger.LogWarning("Admin with id {AdminId} attempted to ban themselves.", adminId);
                return Result.Failure(
                    message: "You cannot ban yourself.",
                    statusCode: StatusCodes.Status400BadRequest,
                    errorCode: ErrorCodes.InvalidOperation
                );
            }

            var user = await _userManager.FindByIdAsync(command.UserId.ToString());

            if (user == null)
            {
                _logger.LogWarning("Admin with id {AdminId} tried to ban non-existent user with id {UserId}.",
                    adminId, command.UserId);
                return Result.Failure(
                    message: "User does not exist.",
                    statusCode: StatusCodes.Status404NotFound,
                    errorCode: ErrorCodes.NotFound
                );
            }

            if (await IsAdminAsync(user))
            {
                _logger.LogWarning("Admin with id {AdminId} attempted to ban admin {UserId}.", adminId, command.UserId);
                return Result.Failure(
                    message: "You cannot ban an administrator.",
                    statusCode: StatusCodes.Status400BadRequest,
                    errorCode: ErrorCodes.InvalidOperation);
            }

            if (user.HasActiveBan())
            {
                _logger.LogWarning("Admin with id {AdminId} tried to ban user with id {UserId} who is already banned.",
                    adminId, command.UserId);
                return Result.Failure(
                    message: "User is already banned.",
                    statusCode: StatusCodes.Status400BadRequest,
                    errorCode: ErrorCodes.UserAlreadyBanned
                );
            }

            await ApplyBanAsync(user, command.Duration, command.Reason);
            await ApprovePendingReportsForUserAsync(user.Id, adminId);

            await _context.SaveChangesAsync(_ct);
            await SendBanEmailSafeAsync(user, command.Reason);

            _logger.LogInformation("Admin with id {AdminId} banned user with id {UserId}.", adminId, command.UserId);

            return Result.Success(
                message: "User banned successfully.",
                statusCode: StatusCodes.Status200OK
            );
        }

        private async Task ApplyBanAsync(User user, TimeSpan? duration, string reason)
        {
            user.Ban(duration, reason);
            await _tokenService.RevokeAllUserRefreshTokensAsync(user.Id, _ct);
        }

        private async Task<bool> ApprovePendingReportsForUserAsync(
            Guid userId,
            Guid adminId,
            Guid? excludeReportId = null)
        {
            var query = _context.UserReports
                .Where(ur => ur.ReportedUserId == userId && ur.Status == ReportStatusEnum.Pending);

            if (excludeReportId.HasValue)
            {
                query = query.Where(ur => ur.Id != excludeReportId.Value);
            }

            var pendingReports = await query.ToListAsync(_ct);

            foreach (var pendingReport in pendingReports)
            {
                var stateMachine = new UserReportStateMachine(pendingReport);
                stateMachine.Approve(adminId);
            }

            return pendingReports.Count > 0;
        }

        private async Task SendBanEmailSafeAsync(User user, string reason)
        {
            if (string.IsNullOrWhiteSpace(user.Email))
                return;

            try
            {
                await _emailService.SendAccountBannedNotificationAsync(
                    user.Email,
                    user.FirstName,
                    reason,
                    user.BannedUntil,
                    _ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send ban notification email to user {UserId}.", user.Id);
            }
        }

        private Task<bool> IsAdminAsync(User user) => _userManager.IsInRoleAsync(user, "Admin");
    }
}
