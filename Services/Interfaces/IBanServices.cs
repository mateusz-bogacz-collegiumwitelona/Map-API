using Domain.Common;
using Services.Command.Ban;

namespace Services.Interfaces
{
    public interface IBanServices
    {
        Task<Result> ReportUserAsync(ReportUserCommand command, Guid currentUserId);
    }
}
