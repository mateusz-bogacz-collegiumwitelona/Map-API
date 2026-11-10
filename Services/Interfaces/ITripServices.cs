using Domain.Common;
using Services.Response.Trip;

namespace Services.Interfaces
{
    public interface ITripServices
    {
        Task<Result<List<TripRouteResponse>>> GetTripRoutesAsync(Guid currentUserId, Guid? targetUserId = null, int? year = null);
        Task<Result<List<VisitedCountryResponse>>> GetVisitedCountriesAsync(Guid currentUserId, Guid? targetUserId = null, int? year = null);
    }
}
