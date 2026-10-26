using Domain.Common;
using Services.Command.User;
using Services.Response.User;

namespace Services.Interfaces
{
    public interface IUserServices
    {
        Task<Result<UserResponse>> GetUserProfileAsync(Guid currentUserId, Guid? targetUserId = null);
        Task<Result> EditUserAsync(Guid userId, EditUserCommand command);
        Task<Result> SetProfileVisibilityAsync(Guid userId, bool isPublic);

    }
}
