using Api.Request.Trip;
using Api.Validators.Validators.Trip;
using Domain.Const;
using FluentValidation.TestHelper;

namespace Tests.Validators.Trip
{
    public class UpsertTripRequestValidatorTest
    {
        private readonly UpsertTripRequestValidator _validator = new();

        [Test]
        public async Task Validate_WhenRequestIsValid_ShouldNotHaveAnyValidationErrors()
        {
            // Arrange
            var request = CreateValidRequest();

            // Act
            var result = await _validator.TestValidateAsync(request);

            // Assert
            await Assert.That(result.IsValid).IsTrue();
        }

        [Test]
        public async Task Validate_WhenTitleIsEmpty_ShouldHaveTripTitleErrorCode()
        {
            // Arrange
            var request = CreateValidRequest() with
            {
                Title = " "
            };

            // Act
            var result = await _validator.TestValidateAsync(request);

            // Assert
            result.ShouldHaveValidationErrorFor(item => item.Title)
                .WithErrorCode(ErrorCodes.TripTitleInvalid);
        }

        [Test]
        public async Task Validate_WhenDescriptionIsTooLong_ShouldHaveTripDescriptionErrorCode()
        {
            // Arrange
            var request = CreateValidRequest() with
            {
                Description = new string('a', 2001)
            };

            // Act
            var result = await _validator.TestValidateAsync(request);

            // Assert
            result.ShouldHaveValidationErrorFor(item => item.Description)
                .WithErrorCode(ErrorCodes.TripDescriptionInvalid);
        }

        [Test]
        public async Task Validate_WhenEndDateIsBeforeStartDate_ShouldHaveTripDateRangeErrorCode()
        {
            // Arrange
            var request = CreateValidRequest() with
            {
                EndDate = new DateTime(
                    2026, 5, 31, 0, 0, 0, DateTimeKind.Utc)
            };

            // Act
            var result = await _validator.TestValidateAsync(request);

            // Assert
            result.ShouldHaveValidationErrorFor(item => item.EndDate)
                .WithErrorCode(ErrorCodes.TripDateRangeInvalid);
        }

        [Test]
        public async Task Validate_WhenTripHasNoSegments_ShouldHaveTripSegmentsErrorCode()
        {
            // Arrange
            var request = CreateValidRequest() with
            {
                Segments = new List<TripSegmentRequest>()
            };

            // Act
            var result = await _validator.TestValidateAsync(request);

            // Assert
            result.ShouldHaveValidationErrorFor(item => item.Segments)
                .WithErrorCode(ErrorCodes.TripSegmentsInvalid);
        }

        [Test]
        public async Task Validate_WhenSegmentsAreDiscontinuous_ShouldHaveTripSegmentsErrorCode()
        {
            // Arrange
            var request = CreateValidRequest();
            var secondSegment = new TripSegmentRequest
            {
                Origin = new TripPointRequest
                {
                    PlaceId = Guid.NewGuid()
                },
                Destination = request.Segments[0].Origin,
                TransportType = "Train"
            };

            request = request with
            {
                Segments = new List<TripSegmentRequest>
        {
            request.Segments[0],
            secondSegment
        }
            };

            // Act
            var result = await _validator.TestValidateAsync(request);

            // Assert
            var continuityError = result.Errors.Single(
                error => error.PropertyName == "Segments");

            await Assert.That(continuityError.ErrorCode)
                .IsEqualTo(ErrorCodes.TripSegmentsInvalid);
        }

        [Test]
        public async Task Validate_WhenPlaceIdIsEmpty_ShouldHaveTripPointErrorCode()
        {
            // Arrange
            var request = CreateValidRequest();
            var segment = request.Segments[0] with
            {
                Destination = request.Segments[0].Destination with
                {
                    PlaceId = Guid.Empty
                }
            };

            request = request with
            {
                Segments = new List<TripSegmentRequest> { segment }
            };

            // Act
            var result = await _validator.TestValidateAsync(request);

            // Assert
            var placeIdError = result.Errors.Single(
                error => error.PropertyName ==
                    "Segments[0].Destination.PlaceId");

            await Assert.That(placeIdError.ErrorCode)
                .IsEqualTo(ErrorCodes.TripPointInvalid);
        }

        [Test]
        public async Task Validate_WhenTransportTypeIsInvalid_ShouldHaveTripTransportTypeErrorCode()
        {
            // Arrange
            var request = CreateValidRequest();
            var segment = request.Segments[0] with
            {
                TransportType = "Helicopter"
            };

            request = request with
            {
                Segments = new List<TripSegmentRequest> { segment }
            };

            // Act
            var result = await _validator.TestValidateAsync(request);

            // Assert
            var transportTypeError = result.Errors.Single(
                error => error.PropertyName ==
                    "Segments[0].TransportType");

            await Assert.That(transportTypeError.ErrorCode)
                .IsEqualTo(ErrorCodes.TripTransportTypeInvalid);
        }

        private static UpsertTripRequest CreateValidRequest()
        {
            return new UpsertTripRequest
            {
                Title = "Podróż po Europie",
                Description = null,
                StartDate = new DateTime(
                    2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                EndDate = null,
                Segments = new List<TripSegmentRequest>
                {
                    new()
                    {
                        Origin = new TripPointRequest
                        {
                            PlaceId = Guid.Parse("00000000-0000-0000-0000-000000000001")
                        },
                        Destination = new TripPointRequest
                        {
                            PlaceId = Guid.Parse("00000000-0000-0000-0000-000000000002")
                        },
                        TransportType = "Train"
                    }
                }
            };
        }
    }
}