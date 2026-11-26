namespace Services.Command.Trip
{
    public sealed record TripPointCommand
    {
        public required Guid PlaceId { get; init; }
    }
}