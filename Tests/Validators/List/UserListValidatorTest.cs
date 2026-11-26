using Api.Request.List;
using Api.Validators.Validators.List;
using Domain.Const;
using FluentValidation.TestHelper;

namespace Tests.Validators.List
{
    public class UserListValidatorTest
    {
        private readonly UserListValidator _validator = new();

        [Test]
        public async Task Validate_WhenAllFieldsAreDefault_ShouldNotHaveAnyValidationErrors()
        {
            // Arrange
            var request = new UserListRequest();

            // Act
            var result = await _validator.TestValidateAsync(request);

            // Assert
            await Assert.That(result.IsValid).IsTrue();
        }

        [Test]
        public async Task Validate_WhenAllFieldsAreValid_ShouldNotHaveAnyValidationErrors()
        {
            // Arrange
            var request = new UserListRequest
            {
                PageNumber = 1,
                PageSize = 10,
                SearchTerm = "Jan Kowalski",
                SortBy = "FirstName",
                SortDescending = true,
                IncludeBanned = true,
                IncludeAdmins = true
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
            var request = new UserListRequest { PageNumber = pageNumber };

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
            var min = new UserListRequest { PageSize = 1 };
            var max = new UserListRequest { PageSize = 100 };

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
            var request = new UserListRequest { PageSize = pageSize };

            // Act
            var result = await _validator.TestValidateAsync(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.PageSize)
                  .WithErrorCode(ErrorCodes.PageSizeInvalid);
        }

        [Test]
        public async Task Validate_WhenSearchTermIsWithinMaxLength_ShouldNotHaveValidationError()
        {
            // Arrange
            var request = new UserListRequest { SearchTerm = new string('a', 100) };

            // Act
            var result = await _validator.TestValidateAsync(request);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.SearchTerm);
        }

        [Test]
        public async Task Validate_WhenSearchTermExceedsMaxLength_ShouldHaveValidationError()
        {
            // Arrange
            var request = new UserListRequest { SearchTerm = new string('a', 101) };

            // Act
            var result = await _validator.TestValidateAsync(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.SearchTerm)
                  .WithErrorCode(ErrorCodes.ValidationError);
        }
    }
}