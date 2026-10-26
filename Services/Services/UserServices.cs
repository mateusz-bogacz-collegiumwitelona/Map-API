using Domain.Common;
using Domain.Const;
using Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Services.Accessors.Interfaces;
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

        public async Task<Result<UserResponse>> GetUserDataAsync(Guid userId)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId, _ct);
            if (user == null)
            {
                _logger.LogWarning("User with ID {UserId} not found.", userId);
                return Result<UserResponse>.Failure(
                    message: $"User not found.",
                    errorCode: ErrorCodes.NotFound,
                    statusCode: StatusCodes.Status404NotFound
                    );
            }

            var response = new UserResponse
            {
                UserId = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Bio = user.Bio,
            };

            return Result<UserResponse>.Success(
                message: "User data retrieved successfully.",
                data: response,
                statusCode: StatusCodes.Status200OK
                );
        }
    }
}
