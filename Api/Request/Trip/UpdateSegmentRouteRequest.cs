namespace Api.Request.Trip
{
    public sealed record UpdateSegmentRouteRequest
    {
        public required List<RoutePointRequest> Coordinates { get; init; }
        public required decimal DistanceKm { get; init; }
        public required bool IsFallbackRoute { get; init; }
    }
}
