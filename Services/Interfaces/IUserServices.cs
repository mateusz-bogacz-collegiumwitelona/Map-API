using Domain.Common;
using Services.Command.User;
using Services.Response.User;

namespace Services.Interfaces
{
    public interface IUserServices
    {
        Task<Result<UserResponse>> GetUserDataAsync(Guid userId);
        Task<Result> EditUserAsync(Guid userId, EditUserCommand command);
    }
}
