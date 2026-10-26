using Domain.Common;
using Services.Response.User;

namespace Services.Interfaces
{
    public interface IUserServices
    {
        Task<Result<UserResponse>> GetUserDataAsync(Guid userId);
    }
}
