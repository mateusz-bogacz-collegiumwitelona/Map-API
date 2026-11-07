namespace Services.Response.Trip
{
    public record TripRouteResponse
    {
        public required Guid TripId { get; init; }
        public required string Title { get; init; }
        public required DateTime StartDate { get; init; }
        public DateTime? EndDate { get; init; }
        public required List<TripRouteSegmentResponse> Segments { get; init; }
    }
}
