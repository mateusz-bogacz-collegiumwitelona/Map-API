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

    public sealed record TripSegmentRequest
    {
        public required TripPointRequest Origin { get; init; }
        public required TripPointRequest Destination { get; init; }
        public required TransportTypeEnum TransportType { get; init; }
    }

    public sealed record TripPointRequest
    {
        public required string Name { get; init; }
        public required double Latitude { get; init; }
        public required double Longitude { get; init; }
    }
}