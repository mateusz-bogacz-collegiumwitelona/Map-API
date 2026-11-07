using Domain.Common;
using Services.Command.Auth;
using Services.Command.User;

namespace Services.Interfaces
{
    public interface IAccountServices
    {
        Task<Result> RequestEmailChangeAsync(Guid userId, ChangeEmailCommand command);
        Task<Result> ConfirmEmailChangeAsync(Guid userId, ConfirmEmailChangeCommand command);
        Task<Result> DeleteAccountAsync(Guid userId);
        Task<Result<string>> RegisterAsync(RegisterCommand command, CancellationToken ct = default);
        Task<Result> ConfirmRegisterAsync(ConfirmEmailCommand command, CancellationToken ct = default);
    }
}
