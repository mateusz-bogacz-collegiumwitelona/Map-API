using Domain.Common;
using Domain.Const;
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

            var user = await GetUserAsync(resolvedUserId);

            if (!isOwner && !user.IsPublic)
            {
                _logger.LogWarning("User {CurrentUserId} attempted to view private profile {TargetUserId}.", currentUserId, resolvedUserId);
                throw new ForbiddenException("This profile is private.");
            }

            var response = new UserResponse
            {
                UserId = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Bio = user.Bio,
                IsPublic = user.IsPublic
            };

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

            user.Bio = string.IsNullOrWhiteSpace(command.Bio) ? null : command.Bio;

            await _context.SaveChangesAsync(_ct);

            return Result.Success(
                message: "User data updated successfully.",
                statusCode: StatusCodes.Status200OK
                );
        }

        private async Task<User> GetUserAsync(Guid userId)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId, _ct);
            if (user == null)
            {
                _logger.LogWarning("User with ID {UserId} not found.", userId);
                throw new NotFoundException($"User with ID '{userId}' was not found.");
            }

            return user;
        }


    }
}
