namespace Api.Request.Trip
{
    public sealed record TripPointRequest : CoordinatesRequest
    {
        public required string Name { get; init; }
    }
}