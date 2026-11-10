namespace Services.Response.Trip
{
    public record VisitedCountryResponse
    {
        public required string CountryCode { get; init; }
        public required string CountryName { get; init; }
        public required List<string> PlaceNames { get; init; }
        public required List<VisitedCountryTripResponse> Trips { get; init; }
    }
}
