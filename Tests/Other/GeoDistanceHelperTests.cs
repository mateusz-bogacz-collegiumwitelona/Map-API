using Domain.Const;
using Services.Helpers;

namespace Tests.Other
{
    public class GeoDistanceHelperTests
    {
        private const double Tolerance = 1e-6;

        [Test]
        public async Task HaversineKm_WhenPointsAreTheSame_ShouldReturnZero()
        {
            // Act
            var distance = GeoDistanceHelper.HaversineKm(50.06, 19.94, 50.06, 19.94);

            // Assert
            await Assert.That(distance).IsEqualTo(0.0);
        }

        [Test]
        public async Task HaversineKm_WhenPointsAreAQuarterOfTheEquatorApart_ShouldReturnAQuarterOfTheCircumference()
        {
            // Act
            var distance = GeoDistanceHelper.HaversineKm(0, 0, 0, 90);

            // Assert
            await Assert.That(distance).IsEqualTo(Math.PI / 2 * GeoConst.EarthRadiusKm).Within(Tolerance);
        }

        [Test]
        public async Task HaversineKm_WhenPointsAreAntipodal_ShouldReturnHalfOfTheCircumference()
        {
            // Act
            var equator = GeoDistanceHelper.HaversineKm(0, 0, 0, 180);
            var poles = GeoDistanceHelper.HaversineKm(-90, 0, 90, 0);

            // Assert
            await Assert.That(equator).IsEqualTo(Math.PI * GeoConst.EarthRadiusKm).Within(Tolerance);
            await Assert.That(poles).IsEqualTo(Math.PI * GeoConst.EarthRadiusKm).Within(Tolerance);
        }

        [Test]
        public async Task HaversineKm_WhenPointsAreOnBothSidesOfTheAntimeridian_ShouldMeasureAcrossIt()
        {
            // Act
            var distance = GeoDistanceHelper.HaversineKm(0, 179, 0, -179);

            // Assert
            await Assert.That(distance).IsEqualTo(Math.PI / 90 * GeoConst.EarthRadiusKm).Within(Tolerance);
        }

        [Test]
        public async Task HaversineKm_ShouldNotDependOnDirection()
        {
            // Act
            var there = GeoDistanceHelper.HaversineKm(50.06, 19.94, 41.90, 12.49);
            var back = GeoDistanceHelper.HaversineKm(41.90, 12.49, 50.06, 19.94);

            // Assert
            await Assert.That(there).IsEqualTo(back).Within(Tolerance);
        }

        [Test]
        public async Task HaversineKm_WhenPointsAreKrakowAndRome_ShouldReturnTheGreatCircleDistance()
        {
            // Act
            var distance = GeoDistanceHelper.HaversineKm(50.06, 19.94, 41.90, 12.49);

            // Assert
            await Assert.That(distance).IsEqualTo(1073.3456118363554).Within(Tolerance);
        }

        [Test]
        public async Task HaversineKm_WhenPointsAreOnTheSameParallelAwayFromTheEquator_ShouldScaleLongitudeByLatitude()
        {
            // Act
            var distance = GeoDistanceHelper.HaversineKm(60, 0, 60, 10);

            // Assert
            await Assert.That(distance).IsEqualTo(555.4451329718406).Within(Tolerance);
        }

        [Test]
        public async Task HaversineKm_WhenPointsAreOneDegreeApartOnAMeridian_ShouldReturnOneDegreeOfArc()
        {
            // Act
            var distance = GeoDistanceHelper.HaversineKm(50, 20, 51, 20);

            // Assert
            await Assert.That(distance).IsEqualTo(Math.PI / 180 * GeoConst.EarthRadiusKm).Within(Tolerance);
        }
    }
}
