using Domain.Common;
using Services.Command.Ban;
using Services.Command.List;
using Services.Response.Ban;

namespace Services.Interfaces
{
    public interface IBanServices
    {
        Task<Result> ReportUserAsync(ReportUserCommand command, Guid currentUserId);
        Task<Result<PagedResult<ReportListResponse>>> GetReportListAsync(ReportListCommand command);
    }
}
