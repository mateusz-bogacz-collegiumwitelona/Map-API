using Api.Request.User;
using Api.Validators.Validators.User;
using Domain.Const;
using FluentValidation.TestHelper;

namespace Tests.Validators.User
{
    public class UserSearchRequestValidatorTest
    {
        private readonly UserSearchRequestValidator _validator = new();

        [Test]
        public async Task Validate_WhenAllFieldsAreNull_ShouldNotHaveAnyValidationErrors()
        {
            // Arrange
            var request = new UserSearchRequest();

            // Act
            var result = await _validator.TestValidateAsync(request);

            // Assert
            await Assert.That(result.IsValid).IsTrue();
        }

        [Test]
        [Arguments("name_asc")]
        [Arguments("name_desc")]
        [Arguments("newest")]
        [Arguments("oldest")]
        [Arguments("NAME_ASC")]
        public async Task Validate_WhenSortIsSupported_ShouldNotHaveValidationError(string sort)
        {
            // Arrange
            var request = new UserSearchRequest { Sort = sort };

            // Act
            var result = await _validator.TestValidateAsync(request);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.Sort);
        }

        [Test]
        [Arguments("")]
        [Arguments("   ")]
        public async Task Validate_WhenSortIsEmptyOrWhitespace_ShouldNotHaveValidationError(string sort)
        {
            // Arrange
            var request = new UserSearchRequest { Sort = sort };

            // Act
            var result = await _validator.TestValidateAsync(request);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.Sort);
        }

        [Test]
        [Arguments("name")]
        [Arguments("popular")]
        [Arguments("newest_asc")]
        public async Task Validate_WhenSortIsNotSupported_ShouldHaveValidationError(string sort)
        {
            // Arrange
            var request = new UserSearchRequest { Sort = sort };

            // Act
            var result = await _validator.TestValidateAsync(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Sort)
                  .WithErrorCode(ErrorCodes.SortInvalid);
        }

        [Test]
        [Arguments(0)]
        [Arguments(-1)]
        public async Task Validate_WhenPageIsNotGreaterThanZero_ShouldHaveValidationError(int page)
        {
            // Arrange
            var request = new UserSearchRequest { Page = page };

            // Act
            var result = await _validator.TestValidateAsync(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Page)
                  .WithErrorCode(ErrorCodes.PageNumberInvalid);
        }

        [Test]
        [Arguments(1)]
        [Arguments(100)]
        public async Task Validate_WhenLimitIsAtBoundaries_ShouldNotHaveValidationError(int limit)
        {
            // Arrange
            var request = new UserSearchRequest { Limit = limit };

            // Act
            var result = await _validator.TestValidateAsync(request);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.Limit);
        }

        [Test]
        [Arguments(0)]
        [Arguments(-1)]
        [Arguments(101)]
        public async Task Validate_WhenLimitIsOutOfRange_ShouldHaveValidationError(int limit)
        {
            // Arrange
            var request = new UserSearchRequest { Limit = limit };

            // Act
            var result = await _validator.TestValidateAsync(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Limit)
                  .WithErrorCode(ErrorCodes.PageSizeInvalid);
        }

        [Test]
        public async Task Validate_WhenQueryIsWithinMaxLength_ShouldNotHaveValidationError()
        {
            // Arrange
            var request = new UserSearchRequest { Q = new string('a', 100) };

            // Act
            var result = await _validator.TestValidateAsync(request);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.Q);
        }

        [Test]
        public async Task Validate_WhenQueryExceedsMaxLength_ShouldHaveValidationError()
        {
            // Arrange
            var request = new UserSearchRequest { Q = new string('a', 101) };

            // Act
            var result = await _validator.TestValidateAsync(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Q)
                  .WithErrorCode(ErrorCodes.ValidationError);
        }
    }
}
