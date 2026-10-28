using Domain.Common;
using Domain.Exceptions;
using Domain.Models;
using Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Services.Accessors.Interfaces;
using Services.Command.User;
using Services.Interfaces;
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

        public async Task<Result<UserResponse>> GetUserProfileAsync(Guid currentUserId, Guid? targetUserId = null)
        {
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
                .FirstOrDefaultAsync(_ct);

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

        public async Task<Result> EditUserAsync(Guid userId, EditUserCommand command)
        {
            var user = await GetUserAsync(userId);

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

            await _context.SaveChangesAsync(_ct);

            _logger.LogInformation("User data updated for user ID: {UserId}", userId);

            return Result.Success(
                message: "User data updated successfully.",
                statusCode: StatusCodes.Status200OK
                );
        }

        public async Task<Result> SetProfileVisibilityAsync(Guid userId, bool isPublic)
        {
            var user = await GetUserAsync(userId);

            if (user.IsPublic == isPublic)
            {
                return Result.Success(
                    message: $"Profile visibility is already {(isPublic ? "public" : "private")}.",
                    statusCode: StatusCodes.Status200OK
                );
            }

            user.IsPublic = isPublic;
            await _context.SaveChangesAsync(_ct);

            _logger.LogInformation("Profile visibility updated for user ID: {UserId} to {Visibility}", userId, isPublic ? "public" : "private");

            return Result.Success(
                message: $"Profile visibility updated to {(isPublic ? "public" : "private")}.",
                statusCode: StatusCodes.Status200OK
            );
        }

        private async Task<User> GetUserAsync(Guid userId)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId, _ct);

            if (user == null)
            {
                throw new NotFoundException($"User with ID '{userId}' was not found.");
            }

            return user;
        }
    }
}
