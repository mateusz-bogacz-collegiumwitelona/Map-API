namespace Services.Command.Trip
{
    public record UpdateSegmentRouteCommand
    {
        public required IReadOnlyList<RoutePointCommand> Coordinates { get; init; }
        public required decimal DistanceKm { get; init; }
        public required bool IsFallbackRoute { get; init; }
    }
}
