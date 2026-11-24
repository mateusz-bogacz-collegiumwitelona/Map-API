using Domain.Common;
using Services.Command.List;
using Services.Command.User;
using Services.Response.User;

namespace Services.Interfaces
{
    public interface IUserServices
    {
        Task<Result<UserResponse>> GetUserProfileAsync(Guid currentUserId, Guid? targetUserId = null, CancellationToken ct = default);
        Task<Result> EditUserAsync(Guid userId, EditUserCommand command, CancellationToken ct = default);
        Task<Result> SetProfileVisibilityAsync(Guid userId, bool isPublic, CancellationToken ct = default);
        Task<Result<PagedResult<UserSummaryResponse>>> SearchUsersAsync(BasicListCommand command, CancellationToken ct = default);

    }
}
