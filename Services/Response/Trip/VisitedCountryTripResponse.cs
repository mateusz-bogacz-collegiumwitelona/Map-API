namespace Services.Response.Trip
{
    public record VisitedCountryTripResponse
    {
        public required Guid TripId { get; init; }
        public required string Title { get; init; }
        public required DateTime StartDate { get; init; }
    }
}
