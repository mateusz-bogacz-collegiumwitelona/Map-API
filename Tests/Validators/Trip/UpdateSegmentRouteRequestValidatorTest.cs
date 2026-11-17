using Api.Request.Trip;
using Api.Validators.Validators.Trip;
using Domain.Const;
using FluentValidation.TestHelper;

namespace Tests.Validators.Trip
{
    public class UpdateSegmentRouteRequestValidatorTest
    {
        private readonly UpdateSegmentRouteRequestValidator _validator = new();

        private static RoutePointRequest Point(double latitude, double longitude)
            => new() { Latitude = latitude, Longitude = longitude };

        private static UpdateSegmentRouteRequest Request(List<RoutePointRequest> coordinates, decimal distanceKm = 1096.50m)
            => new() { Coordinates = coordinates, DistanceKm = distanceKm, IsFallbackRoute = false };

        private static List<RoutePointRequest> Points(int count)
            => Enumerable.Range(0, count).Select(i => Point(50.0, i * 0.0001)).ToList();

        [Test]
        public async Task Validate_WhenAllFieldsAreValid_ShouldNotHaveAnyValidationErrors()
        {
            // Arrange
            var request = Request([Point(50.06, 19.94), Point(46.00, 16.00), Point(41.90, 12.49)]);

            // Act
            var result = await _validator.TestValidateAsync(request);

            // Assert
            await Assert.That(result.IsValid).IsTrue();
        }

        [Test]
        public async Task Validate_WhenCoordinatesAreNull_ShouldHaveRouteInvalidError()
        {
            // Arrange
            var request = Request(null!);

            // Act
            var result = await _validator.TestValidateAsync(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Coordinates)
                  .WithErrorCode(ErrorCodes.RouteInvalid);
        }

        [Test]
        [Arguments(0)]
        [Arguments(1)]
        [Arguments(TripConst.MaxRoutePoints + 1)]
        public async Task Validate_WhenPointCountIsOutOfRange_ShouldHaveRouteInvalidError(int count)
        {
            // Arrange
            var request = Request(Points(count));

            // Act
            var result = await _validator.TestValidateAsync(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Coordinates.Count)
                  .WithErrorCode(ErrorCodes.RouteInvalid);
        }

        [Test]
        [Arguments(TripConst.MinRoutePoints)]
        [Arguments(TripConst.MaxRoutePoints)]
        public async Task Validate_WhenPointCountIsAtBoundaries_ShouldNotHaveValidationError(int count)
        {
            // Arrange
            var request = Request(Points(count));

            // Act
            var result = await _validator.TestValidateAsync(request);

            // Assert
            await Assert.That(result.IsValid).IsTrue();
        }

        [Test]
        [Arguments(-90.0001, 0.0, "Latitude")]
        [Arguments(90.0001, 0.0, "Latitude")]
        [Arguments(0.0, -180.0001, "Longitude")]
        [Arguments(0.0, 180.0001, "Longitude")]
        [Arguments(double.NaN, 0.0, "Latitude")]
        [Arguments(0.0, double.PositiveInfinity, "Longitude")]
        public async Task Validate_WhenAPointIsOutOfRange_ShouldHaveRouteInvalidError(double latitude, double longitude, string property)
        {
            // Arrange
            var request = Request([Point(50.06, 19.94), Point(latitude, longitude), Point(41.90, 12.49)]);

            // Act
            var result = await _validator.TestValidateAsync(request);

            // Assert
            result.ShouldHaveValidationErrorFor($"Coordinates[1].{property}")
                  .WithErrorCode(ErrorCodes.RouteInvalid);
        }

        [Test]
        public async Task Validate_WhenPointsAreAtBoundaries_ShouldNotHaveValidationError()
        {
            // Arrange
            var request = Request([Point(-90, -180), Point(90, 180), Point(0, -180), Point(0, 180)]);

            // Act
            var result = await _validator.TestValidateAsync(request);

            // Assert
            await Assert.That(result.IsValid).IsTrue();
        }

        [Test]
        public async Task Validate_WhenAPointIsNull_ShouldHaveRouteInvalidError()
        {
            // Arrange
            var request = Request([Point(50.06, 19.94), null!, Point(41.90, 12.49)]);

            // Act
            var result = await _validator.TestValidateAsync(request);

            // Assert
            result.ShouldHaveValidationErrorFor("Coordinates[1]")
                  .WithErrorCode(ErrorCodes.RouteInvalid);
        }

        [Test]
        public async Task Validate_WhenPointCountIsOutOfRange_ShouldNotValidateThePoints()
        {
            // Arrange
            var request = Request([Point(91, 0)]);

            // Act
            var result = await _validator.TestValidateAsync(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Coordinates.Count);
            result.ShouldNotHaveValidationErrorFor("Coordinates[0].Latitude");
        }

        [Test]
        [Arguments("0")]
        [Arguments("0.009")]
        [Arguments("-1")]
        [Arguments("40075.01")]
        public async Task Validate_WhenDistanceIsOutOfRange_ShouldHaveDistanceInvalidError(string distance)
        {
            // Arrange
            var request = Request([Point(50.06, 19.94), Point(41.90, 12.49)], decimal.Parse(distance, System.Globalization.CultureInfo.InvariantCulture));

            // Act
            var result = await _validator.TestValidateAsync(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.DistanceKm)
                  .WithErrorCode(ErrorCodes.DistanceInvalid);
        }

        [Test]
        public async Task Validate_WhenDistanceIsAtBoundaries_ShouldNotHaveValidationError()
        {
            // Arrange
            var shortest = Request([Point(50.06, 19.94), Point(41.90, 12.49)], TripConst.MinRouteDistanceKm);
            var longest = Request([Point(50.06, 19.94), Point(41.90, 12.49)], TripConst.MaxRouteDistanceKm);

            // Act
            var shortestResult = await _validator.TestValidateAsync(shortest);
            var longestResult = await _validator.TestValidateAsync(longest);

            // Assert
            shortestResult.ShouldNotHaveValidationErrorFor(x => x.DistanceKm);
            longestResult.ShouldNotHaveValidationErrorFor(x => x.DistanceKm);
        }
    }
}
