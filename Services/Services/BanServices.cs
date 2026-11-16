using Domain.Common;
using Domain.Const;
using Domain.Exceptions;
using Domain.Models;
using Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Services.Accessors.Interfaces;
using Services.Command.Ban;
using Services.Interfaces;

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

            return Result.Success(
                message: "User reported successfully.",
                statusCode: StatusCodes.Status200OK
                );
        }
    }
}
