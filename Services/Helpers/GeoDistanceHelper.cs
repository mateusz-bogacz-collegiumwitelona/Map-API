using Domain.Const;

namespace Services.Helpers
{
    public static class GeoDistanceHelper
    {
        public static double HaversineKm(double fromLatitude, double fromLongitude, double toLatitude, double toLongitude)
        {
            var fromLatitudeRad = double.DegreesToRadians(fromLatitude);
            var toLatitudeRad = double.DegreesToRadians(toLatitude);
            var sinHalfDeltaLatitude = Math.Sin(double.DegreesToRadians(toLatitude - fromLatitude) / 2);
            var sinHalfDeltaLongitude = Math.Sin(double.DegreesToRadians(toLongitude - fromLongitude) / 2);

            var h = sinHalfDeltaLatitude * sinHalfDeltaLatitude
                + Math.Cos(fromLatitudeRad) * Math.Cos(toLatitudeRad) * sinHalfDeltaLongitude * sinHalfDeltaLongitude;

            return 2 * GeoConst.EarthRadiusKm * Math.Asin(Math.Sqrt(Math.Min(1.0, h)));
        }
    }
}
