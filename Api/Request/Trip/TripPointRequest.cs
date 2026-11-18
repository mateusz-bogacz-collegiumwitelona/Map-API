namespace Api.Request.Trip
{
    public sealed record TripPointRequest
    {
        public required string Name { get; init; }
        public required double Latitude { get; init; }
        public required double Longitude { get; init; }
    }
}
