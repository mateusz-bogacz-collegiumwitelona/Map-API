using Domain.Common;
using Services.Command.List;
using Services.Command.Trip;
using Services.Response.Trip;

namespace Services.Interfaces
{
    public interface ITripServices
    {
        Task<Result<List<TripRouteResponse>>> GetTripRoutesAsync(Guid currentUserId, Guid? targetUserId = null, int? year = null);
        Task<Result<List<VisitedCountryResponse>>> GetVisitedCountriesAsync(Guid currentUserId, Guid? targetUserId = null, int? year = null);
        Task<Result<PagedResult<TripSummaryResponse>>> GetUserTripsAsync(BasicListCommand command, Guid userId, CancellationToken ct = default);
        Task<Result> UpdateSegmentRouteAsync(Guid userId, Guid segmentId, UpdateSegmentRouteCommand command);
    }
}
