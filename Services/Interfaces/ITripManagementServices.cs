using Domain.Common;
using Services.Command.Trip;
using Services.Response.Trip;

namespace Services.Interfaces
{
    public interface ITripManagementServices
    {
        Task<Result<TripDetailsResponse>> CreateTripAsync(
            Guid userId,
            UpsertTripCommand command);

        Task<Result<TripDetailsResponse>> UpdateTripAsync(
            Guid userId,
            Guid tripId,
            UpsertTripCommand command);

        Task<Result<TripDetailsResponse>> GetTripDetailsAsync(
            Guid userId,
            Guid tripId);

        Task<Result> DeleteTripAsync(
            Guid userId,
            Guid tripId);
    }
}