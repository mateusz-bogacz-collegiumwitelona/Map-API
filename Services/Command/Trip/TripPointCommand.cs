namespace Services.Command.Trip
{
    public sealed record TripPointCommand
    {
        public required string Name { get; init; }
        public required double Latitude { get; init; }
        public required double Longitude { get; init; }
    }
}