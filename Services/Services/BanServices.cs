using Domain.Common;
using Domain.Const;
using Domain.Models;
using Infrastructure;
using Microsoft.AspNetCore.Http;
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

        private CancellationToken _ct => _ctAccessor.Token;

        public BanServices(
            AppDbContext context,
            ILogger<BanServices> logger,
            ICancellationTokenAccessor ctAccessor)
        {
            _context = context;
            _logger = logger;
            _ctAccessor = ctAccessor;
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
    }
}
