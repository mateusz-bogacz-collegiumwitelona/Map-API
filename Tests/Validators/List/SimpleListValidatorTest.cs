using Api.Request.List;
using Api.Validators.Validators.List;
using Domain.Const;
using FluentValidation.TestHelper;

namespace Tests.Validators.List
{
    public class SimpleListValidatorTest
    {
        private readonly SimpleListValidator _validator = new();

        [Test]
        public async Task Validate_WhenAllFieldsAreNull_ShouldNotHaveAnyValidationErrors()
        {
            // Arrange
            var request = new SimpleListRequest();

            // Act
            var result = await _validator.TestValidateAsync(request);

            // Assert
            await Assert.That(result.IsValid).IsTrue();
        }

        [Test]
        public async Task Validate_WhenAllFieldsAreValid_ShouldNotHaveAnyValidationErrors()
        {
            // Arrange
            var request = new SimpleListRequest
            {
                PageNumber = 1,
                PageSize = 10,
                SortBy = "createdAt",
                SortDescending = false
            };

            // Act
            var result = await _validator.TestValidateAsync(request);

            // Assert
            await Assert.That(result.IsValid).IsTrue();
        }

        [Test]
        [Arguments(0)]
        [Arguments(-1)]
        public async Task Validate_WhenPageNumberIsNotGreaterThanZero_ShouldHaveValidationError(int pageNumber)
        {
            // Arrange
            var request = new SimpleListRequest { PageNumber = pageNumber };

            // Act
            var result = await _validator.TestValidateAsync(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.PageNumber)
                  .WithErrorCode(ErrorCodes.PageNumberInvalid);
        }

        [Test]
        public async Task Validate_WhenPageSizeIsAtBoundaries_ShouldNotHaveValidationError()
        {
            // Arrange
            var min = new SimpleListRequest { PageSize = 1 };
            var max = new SimpleListRequest { PageSize = 100 };

            // Act
            var minResult = await _validator.TestValidateAsync(min);
            var maxResult = await _validator.TestValidateAsync(max);

            // Assert
            minResult.ShouldNotHaveValidationErrorFor(x => x.PageSize);
            maxResult.ShouldNotHaveValidationErrorFor(x => x.PageSize);
        }

        [Test]
        [Arguments(0)]
        [Arguments(-1)]
        [Arguments(101)]
        public async Task Validate_WhenPageSizeIsOutOfRange_ShouldHaveValidationError(int pageSize)
        {
            // Arrange
            var request = new SimpleListRequest { PageSize = pageSize };

            // Act
            var result = await _validator.TestValidateAsync(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.PageSize)
                  .WithErrorCode(ErrorCodes.PageSizeInvalid);
        }
    }
}