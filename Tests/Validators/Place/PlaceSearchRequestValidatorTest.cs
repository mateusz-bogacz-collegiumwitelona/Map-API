using Api.Request.Place;
using Api.Validators.Validators.Place;
using Domain.Const;
using FluentValidation.TestHelper;

namespace Tests.Validators.Place
{
    public class PlaceSearchRequestValidatorTest
    {
        private readonly PlaceSearchRequestValidator _validator = new();

        [Test]
        public async Task Validate_WhenQueryIsValid_ShouldHaveNoValidationErrors()
        {
            // Arrange
            var request = new PlaceSearchRequest { Query = "War" };

            // Act
            var result = await _validator.TestValidateAsync(request);

            // Assert
            await Assert.That(result.IsValid).IsTrue();
        }

        [Test]
        public async Task Validate_WhenQueryIsTooShort_ShouldHaveValidationError()
        {
            // Arrange
            var request = new PlaceSearchRequest { Query = "W" };

            // Act
            var result = await _validator.TestValidateAsync(request);

            // Assert
            result.ShouldHaveValidationErrorFor(item => item.Query)
                .WithErrorCode(ErrorCodes.PlaceSearchQueryInvalid);
        }

        [Test]
        public async Task Validate_WhenQueryIsEmpty_ShouldHaveValidationError()
        {
            // Arrange
            var request = new PlaceSearchRequest { Query = "  " };

            // Act
            var result = await _validator.TestValidateAsync(request);

            // Assert
            result.ShouldHaveValidationErrorFor(item => item.Query)
                .WithErrorCode(ErrorCodes.PlaceSearchQueryInvalid);
        }
    }
}