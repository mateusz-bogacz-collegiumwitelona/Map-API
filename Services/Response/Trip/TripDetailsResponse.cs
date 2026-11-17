using Domain.Enum;

namespace Services.Response.Trip
{
    public sealed record TripDetailsResponse
    {
        public required Guid Id { get; init; }
        public required string Title { get; init; }
        public string? Description { get; init; }
        public required DateTime StartDate { get; init; }
        public DateTime? EndDate { get; init; }
        public required List<TripDetailsSegmentResponse> Segments { get; init; }
    }

    public sealed record TripDetailsSegmentResponse
    {
        public required int OrderIndex { get; init; }
        public required TransportTypeEnum TransportType { get; init; }
        public required decimal DistanceKm { get; init; }
        public required bool IsFallbackRoute { get; init; }
        public required TripPointResponse Origin { get; init; }
        public required TripPointResponse Destination { get; init; }
    }

    public sealed record TripPointResponse
    {
        public required string Name { get; init; }
        public required double Latitude { get; init; }
        public required double Longitude { get; init; }
    }
}