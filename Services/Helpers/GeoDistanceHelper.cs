using Domain.Const;

namespace Services.Helpers
{
    public static class GeoDistanceHelper
    {
        public static double HaversineKm(double fromLatitude, double fromLongitude, double toLatitude, double toLongitude)
        {
            var fromLatitudeRad = ToRadians(fromLatitude);
            var toLatitudeRad = ToRadians(toLatitude);
            var deltaLatitude = ToRadians(toLatitude - fromLatitude);
            var deltaLongitude = ToRadians(toLongitude - fromLongitude);

            var h = Math.Pow(Math.Sin(deltaLatitude / 2), 2)
                + Math.Cos(fromLatitudeRad) * Math.Cos(toLatitudeRad) * Math.Pow(Math.Sin(deltaLongitude / 2), 2);

            return 2 * GeoConst.EarthRadiusKm * Math.Asin(Math.Sqrt(Math.Min(1.0, h)));
        }

        private static double ToRadians(double degrees) => degrees * Math.PI / 180.0;
    }
}
