using Domain.Common;
using Services.Command.Trip;

namespace Services.Interfaces
{
    public interface ITripSegmentServices
    {
        Task<Result> UpdateRouteAsync(Guid userId, Guid segmentId, UpdateSegmentRouteCommand command);
    }
}
