using Domain.Enum;

namespace Api.Request.Trip
{
    public sealed record UpsertTripRequest
    {
        public required string Title { get; init; }
        public string? Description { get; init; }
        public required DateTime StartDate { get; init; }
        public DateTime? EndDate { get; init; }
        public required List<TripSegmentRequest> Segments { get; init; }
    }
}