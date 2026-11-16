using Domain.Common;
using Domain.Const;
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
using Services.QueryExtension;
using Services.Response.Ban;

namespace Services.Services
{
    public class BanServices : IBanServices
    {
        private readonly AppDbContext _context;
        private readonly ILogger<BanServices> _logger;
        private readonly ICancellationTokenAccessor _ctAccessor;
        private readonly UserManager<User> _userManager;

        private CancellationToken _ct => _ctAccessor.Token;

        public BanServices(
            AppDbContext context,
            ILogger<BanServices> logger,
            ICancellationTokenAccessor ctAccessor,
            UserManager<User> userManager)
        {
            _context = context;
            _logger = logger;
            _ctAccessor = ctAccessor;
            _userManager = userManager;
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

            if (!await _context.Users.AnyAsync(u => u.Id == command.UserId, _ct))
            {
                _logger.LogWarning("User {UserId} attempted to report a non-existent user {TargetUserId}.", command.UserId, currentUserId);
                return Result.Failure(
                      message: "The user you are trying to report does not exist.",
                      errorCode: ErrorCodes.UserNotFound,
                      statusCode: StatusCodes.Status404NotFound);
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
                _logger.LogWarning("Admin with id {adminId} want resolve report with id {reportId} with dosn't exist.", 
                    adminId, command.Id);
                return Result.Failure(
                    message: "Report does not exist.",
                    statusCode: StatusCodes.Status400BadRequest,
                    errorCode: ErrorCodes.ReportNotFound
                    );
            }

            var stateMachine = new UserReportStateMachine(report);

            if (!stateMachine.CanReject() || !stateMachine.CanApprove())
            {
                _logger.LogWarning("Admin with id {adminId} want resolve report with id {reportId} with is resolved.",
                    adminId, command.Id);

                return Result.Failure(
                    message: "Report has already been resolved.",
                    statusCode: StatusCodes.Status400BadRequest,
                    errorCode: ErrorCodes.ReportAlreadyResolved
                );
            }

            if (!command.IsBanned)
            {
                stateMachine.Reject(adminId);
            } 
            else
            {
                if (report.ReportedUser.HasActiveBan())
                {
                    _logger.LogWarning("Admin with id {adminId} want ban user with id {userId} with is banned now.",
                        adminId, report.ReportedUserId);
                    return Result.Failure(
                        message: "Reported user is already banned.",
                        statusCode: StatusCodes.Status400BadRequest,
                        errorCode: ErrorCodes.UserAlreadyBanned
                    );
                }

                stateMachine.Approve(adminId);

                var banResult = await ApplyBanAsync(report.ReportedUser, command.Duration, report.Reason);

                if (!banResult.Succeeded)
                {
                    _logger.LogError("Failed to ban user {UserId} for report {ReportId}", report.ReportedUserId, report.Id);
                    throw new InternalServerException("Failed to ban reported user.");
                }
            }

            await _context.SaveChangesAsync(_ct);

            return Result.Success(
                message: "Report resolved successfully.",
                statusCode: StatusCodes.Status200OK
            );
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
                    errorCode: ErrorCodes.UserNotFound
                );
            }

            if (!user.HasActiveBan())
            {
                _logger.LogWarning("Admin with id {AdminId} tried to unban user with id {UserId} who is not currently banned.",
                    adminId, userId);

                return Result.Failure(
                    message: "User is not currently banned.",
                    statusCode: StatusCodes.Status400BadRequest,
                    errorCode: ErrorCodes.UserNotBanned
                );
            }

            user.IsBanned = false;
            user.BannedReason = null;
            user.BannedUntil = null;

            var unbanResult = await _userManager.SetLockoutEndDateAsync(user, null);
            if (!unbanResult.Succeeded)
            {
                _logger.LogError("Failed to unban user {UserId} by admin {AdminId}.", userId, adminId);
                throw new InternalServerException("Failed to unban user.");
            }

            _logger.LogInformation("Admin with id {AdminId} unbanned user with id {UserId}.", adminId, userId);
            return Result.Success(
                message: "User unbanned successfully.",
                statusCode: StatusCodes.Status200OK
            );
        }

        private async Task<IdentityResult> ApplyBanAsync(User user, TimeSpan? duration, string reason)
        {
            if (!user.LockoutEnabled)
            {
                await _userManager.SetLockoutEnabledAsync(user, true);
            }

            DateTime? bannedUntil = duration.HasValue
                ? DateTime.UtcNow.Add(duration.Value)
                : null;

            DateTimeOffset lockoutEnd = bannedUntil.HasValue
                ? new DateTimeOffset(bannedUntil.Value, TimeSpan.Zero)
                : DateTimeOffset.MaxValue;

            user.IsBanned = true;
            user.BannedUntil = bannedUntil;
            user.BannedReason = reason;

            var result = await _userManager.SetLockoutEndDateAsync(user, lockoutEnd);
            if (result.Succeeded)
            {
                await _userManager.UpdateSecurityStampAsync(user);
            }

            return result;
        }
    }
}
