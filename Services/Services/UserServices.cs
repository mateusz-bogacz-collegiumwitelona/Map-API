using Domain.Common;
using Domain.Const;
using Domain.Exceptions;
using Domain.Models;
using Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Services.Accessors.Interfaces;
using Services.Command.List;
using Services.Command.User;
using Services.Helpers;
using Services.Interfaces;
using Services.QueryExtension;
using Services.Response.User;

namespace Services.Services
{
    public class UserServices : IUserServices
    {
        private readonly AppDbContext _context;
        private readonly ILogger<UserServices> _logger;
        private readonly ICancellationTokenAccessor _ctAccessor;
        private CancellationToken _ct => _ctAccessor.Token;

        public UserServices(
            AppDbContext context,
            ILogger<UserServices> logger,
            ICancellationTokenAccessor ctAccessor)
        {
            _context = context;
            _logger = logger;
            _ctAccessor = ctAccessor;
        }

        public async Task<Result<UserResponse>> GetUserProfileAsync(
            Guid currentUserId,
            Guid? targetUserId = null,
            CancellationToken ct = default
            )
        {
            var token = _ctAccessor.ResolveCancellationToken(ct);

            var resolvedUserId = targetUserId ?? currentUserId;
            var isOwner = resolvedUserId == currentUserId;

            var user = await _context.Users
                .AsNoTracking()
                .Where(u => u.Id == resolvedUserId)
                .Select(u => new
                {
                    u.Id,
                    u.FirstName,
                    u.LastName,
                    u.Bio,
                    u.IsPublic
                })
                .FirstOrDefaultAsync(token);

            if (user == null || (!isOwner && !user.IsPublic))
            {
                throw new NotFoundException($"User with ID '{resolvedUserId}' was not found.");
            }

            var response = new UserResponse
            {
                UserId = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Bio = user.Bio,
                IsPublic = user.IsPublic
            };

            _logger.LogInformation("User profile retrieved for user ID: {UserId}, IsOwner: {IsOwner}", resolvedUserId, isOwner);
            return Result<UserResponse>.Success(
                message: "User data retrieved successfully.",
                data: response,
                statusCode: StatusCodes.Status200OK
            );
        }

        public async Task<Result> EditUserAsync(
            Guid userId,
            EditUserCommand command,
            CancellationToken ct = default
            )
        {
            var token = _ctAccessor.ResolveCancellationToken(ct);
            var user = await GetUserAsync(userId, token);

            if (!string.IsNullOrWhiteSpace(command.FirstName))
            {
                user.FirstName = command.FirstName;
            }

            if (!string.IsNullOrWhiteSpace(command.LastName))
            {
                user.LastName = command.LastName;
            }

            if (command.Bio is not null)
            {
                user.Bio = string.IsNullOrWhiteSpace(command.Bio) ? null : command.Bio.Trim();
            }

            await _context.SaveChangesAsync(token);

            _logger.LogInformation("User data updated for user ID: {UserId}", userId);

            return Result.Success(
                message: "User data updated successfully.",
                statusCode: StatusCodes.Status200OK
                );
        }

        public async Task<Result> SetProfileVisibilityAsync(
            Guid userId,
            bool isPublic,
            CancellationToken ct = default)
        {
            var token = _ctAccessor.ResolveCancellationToken(ct);
            var user = await GetUserAsync(userId, token);

            if (user.IsPublic == isPublic)
            {
                return Result.Success(
                    message: $"Profile visibility is already {(isPublic ? "public" : "private")}.",
                    statusCode: StatusCodes.Status200OK
                );
            }

            user.IsPublic = isPublic;
            await _context.SaveChangesAsync(token);

            _logger.LogInformation("Profile visibility updated for user ID: {UserId} to {Visibility}", userId, isPublic ? "public" : "private");

            return Result.Success(
                message: $"Profile visibility updated to {(isPublic ? "public" : "private")}.",
                statusCode: StatusCodes.Status200OK
            );
        }

        private async Task<User> GetUserAsync(Guid userId, CancellationToken token)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId, token);

            if (user == null)
            {
                throw new NotFoundException($"User with ID '{userId}' was not found.");
            }

            return user;
        }

        public async Task<Result<PagedResult<UserListResponse>>> GetUserListAsync(UserListCommand command)
            => await _context.Users
                .AsNoTracking()
                .ApplySearch(command.SearchTerm)
                .ApplyFilter(command.IncludeBanned, command.IncludeAdmins, _context)
                .ApplySorting(command.SortBy, command.SortDescending)
                .Select(u => new UserListResponse
                {
                    Id = u.Id,
                    FirstName = u.FirstName,
                    LastName = u.LastName,

                    Role = _context.UserRoles
                        .Where(ur => ur.UserId == u.Id)
                        .Join(_context.Roles,
                            ur => ur.RoleId,
                            r => r.Id,
                            (ur, r) => r.Name)
                        .FirstOrDefault() ?? string.Empty,

                    IsBanned = u.IsBanned,
                    BannedUntil = u.BannedUntil,
                    CreatedAt = u.CreatedAt,
                    UpdatedAt = u.UpdatedAt
                })
            .ToPagedResultAsync(command.PageNumber, command.PageSize, _logger, "UserList", _ct);

        public async Task<Result<UserDetailResponse>> GetUserDetailAsync(Guid userId)
        {
            var user = await _context.Users
                .AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => new
                {
                    u.Id,
                    u.FirstName,
                    u.LastName,
                    u.Email,
                    Role = _context.UserRoles
                        .Where(ur => ur.UserId == u.Id)
                        .Join(_context.Roles,
                            ur => ur.RoleId,
                            r => r.Id,
                            (ur, r) => r.Name)
                        .FirstOrDefault() ?? string.Empty,
                    u.IsPublic
                })
                .FirstOrDefaultAsync(_ct);

            if (user == null)
            {
                _logger.LogWarning("User with ID {UserId} not found.", userId);
                return Result<UserDetailResponse>.Failure(
                    message: $"User with ID '{userId}' was not found.",
                    statusCode: StatusCodes.Status404NotFound,
                    errorCode: ErrorCodes.NotFound
                );
            }

            if (string.IsNullOrWhiteSpace(user.Email))
            {
                _logger.LogError("Data integrity violation: User with ID {UserId} does not have an email address.", userId);
                throw new InternalServerException("User email is required.");
            }

            var response = new UserDetailResponse
            {
                Id = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                Role = user.Role,
                IsPublic = user.IsPublic
            };

            return Result<UserDetailResponse>.Success(
                message: "User details retrieved successfully.",
                data: response,
                statusCode: StatusCodes.Status200OK
            );
        }

        public async Task<Result<UserBanDetailResponse>> GetUserBanDetailAsync(Guid userId)
        {
            var now = DateTime.UtcNow;

            var response = await _context.Users
                .AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => new UserBanDetailResponse
                {
                    IsBanned = u.IsBanned && (u.BannedUntil == null || u.BannedUntil > now),
                    BannedUntil = u.BannedUntil,
                    BannedReason = u.BannedReason
                })
                .FirstOrDefaultAsync(_ct);

            if (response == null)
            {
                _logger.LogWarning("User with ID {UserId} not found.", userId);
                return Result<UserBanDetailResponse>.Failure(
                    message: $"User with ID '{userId}' was not found.",
                    statusCode: StatusCodes.Status404NotFound,
                    errorCode: ErrorCodes.NotFound
                );
            }

            return Result<UserBanDetailResponse>.Success(
                message: "User ban details retrieved successfully.",
                data: response,
                statusCode: StatusCodes.Status200OK
            );
        }

        public async Task<Result<PagedResult<UserReportListResponse>>> GetUserReportListAsync(Guid userId, SimpleListCommand command)
        {
            var userExists = await _context.Users
                .AsNoTracking()
                .AnyAsync(u => u.Id == userId, _ct);

            if (!userExists)
            {
                _logger.LogWarning("User with ID {UserId} not found.", userId);
                return Result<PagedResult<UserReportListResponse>>.Failure(
                    message: $"User with ID '{userId}' was not found.",
                    statusCode: StatusCodes.Status404NotFound,
                    errorCode: ErrorCodes.NotFound
                );
            }

           return await _context.UserReports
                .AsNoTracking()
                .ApplySorting(command.SortBy, command.SortDescending)
                .Where(r => r.ReportedUserId == userId)
                .Select(r => new UserReportListResponse
                {
                    Id = r.Id,
                    Reason = r.Reason,
                    Status = r.Status,
                    CreatedAt = r.CreatedAt,
                    ResolvedAt = r.ResolvedAt
                })
            .ToPagedResultAsync(command.PageNumber, command.PageSize, _logger, "UserReportList", _ct);
        }
    }
}
