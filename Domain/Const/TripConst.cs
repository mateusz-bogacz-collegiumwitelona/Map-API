namespace Domain.Const
{
    public static class TripConst
    {
        public const int MinFilterYear = 1950;
        public const int MaxFilterYearsAhead = 1;

        public const int MinRoutePoints = 2;
        public const int MaxRoutePoints = 10000;
        public const decimal MinRouteDistanceKm = 0.01m;
        public const decimal MaxRouteDistanceKm = 40075m;
        public const double RouteEndpointToleranceKm = 10.0;
        public const double RouteDistanceTolerance = 0.01;
        public const double RouteDistanceRoundingKm = 0.005;
    }
}
