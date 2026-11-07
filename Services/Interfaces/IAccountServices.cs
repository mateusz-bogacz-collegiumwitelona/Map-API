using Domain.Common;
using Services.Command.Account;
using Services.Command.Auth;

namespace Services.Interfaces
{
    public interface IAccountServices
    {
        Task<Result> RequestEmailChangeAsync(Guid userId, ChangeEmailCommand command);
        Task<Result> ConfirmEmailChangeAsync(Guid userId, ConfirmEmailChangeCommand command);
        Task<Result> DeleteAccountAsync(Guid userId);
        Task<Result> RegisterAsync(RegisterCommand command, CancellationToken ct = default);
        Task<Result> ConfirmRegisterAsync(ConfirmEmailCommand command, CancellationToken ct = default);
    }
}
