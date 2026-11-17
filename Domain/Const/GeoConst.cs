namespace Domain.Const
{
    public static class GeoConst
    {
        public static readonly int SRID = 4326;
        public const double RouteSimplificationToleranceDegrees = 0.001;
        public const double EarthRadiusKm = 6371.0;
        public const double MinLatitude = -90.0;
        public const double MaxLatitude = 90.0;
        public const double MinLongitude = -180.0;
        public const double MaxLongitude = 180.0;
    }
}
