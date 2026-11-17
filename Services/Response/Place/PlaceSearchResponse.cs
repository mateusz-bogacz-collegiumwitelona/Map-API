namespace Services.Response.Place
{
    public sealed record PlaceSearchResponse
    {
        public required Guid Id { get; init; }
        public required string Name { get; init; }
        public required string CountryName { get; init; }
        public required string CountryCode { get; init; }
        public required double Latitude { get; init; }
        public required double Longitude { get; init; }
    }
}