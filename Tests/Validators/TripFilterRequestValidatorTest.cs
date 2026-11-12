using Api.Request.Trip;
using Api.Validators.Trip;
using Domain.Const;
using FluentValidation.TestHelper;

namespace Tests.Validators
{
    public class TripFilterRequestValidatorTest
    {
        private readonly TripFilterRequestValidator _validator = new();

        [Test]
        public async Task Validate_WhenAllFieldsAreNull_ShouldNotHaveAnyValidationErrors()
        {
            // Arrange
            var request = new TripFilterRequest();

            // Act
            var result = await _validator.TestValidateAsync(request);

            // Assert
            await Assert.That(result.IsValid).IsTrue();
        }

        [Test]
        public async Task Validate_WhenAllFieldsAreValid_ShouldNotHaveAnyValidationErrors()
        {
            // Arrange
            var request = new TripFilterRequest { UserId = Guid.NewGuid(), Year = 2024 };

            // Act
            var result = await _validator.TestValidateAsync(request);

            // Assert
            await Assert.That(result.IsValid).IsTrue();
        }

        [Test]
        public async Task Validate_WhenUserIdIsEmptyGuid_ShouldHaveValidationError()
        {
            // Arrange
            var request = new TripFilterRequest { UserId = Guid.Empty };

            // Act
            var result = await _validator.TestValidateAsync(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.UserId)
                  .WithErrorCode(ErrorCodes.User.UserIdInvalid);
        }

        [Test]
        public async Task Validate_WhenYearIsAtBoundaries_ShouldNotHaveValidationError()
        {
            // Arrange
            var oldest = new TripFilterRequest { Year = TripConst.MinFilterYear };
            var newest = new TripFilterRequest { Year = DateTime.UtcNow.Year + TripConst.MaxFilterYearsAhead };

            // Act
            var oldestResult = await _validator.TestValidateAsync(oldest);
            var newestResult = await _validator.TestValidateAsync(newest);

            // Assert
            oldestResult.ShouldNotHaveValidationErrorFor(x => x.Year);
            newestResult.ShouldNotHaveValidationErrorFor(x => x.Year);
        }

        [Test]
        public async Task Validate_WhenYearIsBeforeMinimum_ShouldHaveValidationError()
        {
            // Arrange
            var request = new TripFilterRequest { Year = TripConst.MinFilterYear - 1 };

            // Act
            var result = await _validator.TestValidateAsync(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Year)
                  .WithErrorCode(ErrorCodes.Trip.YearInvalid);
        }

        [Test]
        public async Task Validate_WhenYearIsTooFarInFuture_ShouldHaveValidationError()
        {
            // Arrange
            var request = new TripFilterRequest { Year = DateTime.UtcNow.Year + TripConst.MaxFilterYearsAhead + 1 };

            // Act
            var result = await _validator.TestValidateAsync(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Year)
                  .WithErrorCode(ErrorCodes.Trip.YearInvalid);
        }
    }
}
