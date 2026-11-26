namespace Api.Request.Trip
{
    public sealed record TripPointRequest
    {
        public required Guid PlaceId { get; init; }
    }
}