using Api.Mappers;
using Api.Request.Trip;

namespace Tests.Mappers
{
    public class TripMapperTest
    {
        private readonly TripMapper _mapper = new();

        [Test]
        public async Task MapUpdateSegmentRoute_ShouldMapPointsInOrderAndFallbackFlag()
        {
            // Arrange
            var request = new UpdateSegmentRouteRequest
            {
                Coordinates =
                [
                    new RoutePointRequest { Latitude = 50.06, Longitude = 19.94 },
                    new RoutePointRequest { Latitude = 46.00, Longitude = 16.00 },
                    new RoutePointRequest { Latitude = 41.90, Longitude = 12.49 }
                ],
                DistanceKm = 1096.50m,
                IsFallbackRoute = true
            };

            // Act
            var result = _mapper.MapUpdateSegmentRoute(request);

            // Assert
            await Assert.That(result.Coordinates.Count).IsEqualTo(3);
            await Assert.That(result.Coordinates[0].Latitude).IsEqualTo(50.06);
            await Assert.That(result.Coordinates[0].Longitude).IsEqualTo(19.94);
            await Assert.That(result.Coordinates[1].Latitude).IsEqualTo(46.00);
            await Assert.That(result.Coordinates[1].Longitude).IsEqualTo(16.00);
            await Assert.That(result.Coordinates[2].Latitude).IsEqualTo(41.90);
            await Assert.That(result.Coordinates[2].Longitude).IsEqualTo(12.49);
            await Assert.That(result.DistanceKm).IsEqualTo(1096.50m);
            await Assert.That(result.IsFallbackRoute).IsTrue();
        }

        [Test]
        [Arguments("12.344", "12.34")]
        [Arguments("12.345", "12.35")]
        [Arguments("12.3", "12.3")]
        [Arguments("1096.505", "1096.51")]
        public async Task MapUpdateSegmentRoute_ShouldRoundDistanceToTwoDecimalPlaces(string distance, string expected)
        {
            // Arrange
            var request = new UpdateSegmentRouteRequest
            {
                Coordinates =
                [
                    new RoutePointRequest { Latitude = 50.06, Longitude = 19.94 },
                    new RoutePointRequest { Latitude = 41.90, Longitude = 12.49 }
                ],
                DistanceKm = decimal.Parse(distance, System.Globalization.CultureInfo.InvariantCulture),
                IsFallbackRoute = false
            };

            // Act
            var result = _mapper.MapUpdateSegmentRoute(request);

            // Assert
            await Assert.That(result.DistanceKm).IsEqualTo(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture));
        }
    }
}
