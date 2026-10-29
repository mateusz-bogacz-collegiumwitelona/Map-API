using Api.Request.User;
using Api.Validators.User;
using Domain.Const;
using FluentValidation.TestHelper;

namespace Tests.Validators
{
    public class EditUserValidatorTest
    {
        private readonly EditUserValidator _validator = new();

        [Test]
        public async Task Validate_WhenAllFieldsAreNull_ShouldNotHaveAnyValidationErrors()
        {
            // Arrange
            var request = new EditUserRequest
            {
                FirstName = null,
                LastName = null,
                Bio = null
            };

            // Act
            var result = await _validator.TestValidateAsync(request);

            // Assert
            await Assert.That(result.IsValid).IsTrue();
        }

        [Test]
        public async Task Validate_WhenAllFieldsAreValid_ShouldNotHaveAnyValidationErrors()
        {
            // Arrange
            var request = new EditUserRequest
            {
                FirstName = "Jan",
                LastName = "Kowalski",
                Bio = "Poprawny opis profilu"
            };

            // Act
            var result = await _validator.TestValidateAsync(request);

            // Assert
            await Assert.That(result.IsValid).IsTrue();
        }

        [Test]
        [Arguments("")]
        [Arguments("   ")]
        public async Task Validate_WhenFirstNameIsEmptyOrWhitespace_ShouldHaveValidationError(string invalidFirstName)
        {
            // Arrange
            var request = new EditUserRequest { FirstName = invalidFirstName };

            // Act
            var result = await _validator.TestValidateAsync(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.FirstName)
                  .WithErrorCode(ErrorCodes.User.FirstNameInvalid);
        }

        [Test]
        public async Task Validate_WhenFirstNameIsShorterThanTwoCharacters_ShouldHaveValidationError()
        {
            // Arrange
            var request = new EditUserRequest { FirstName = "A" };

            // Act
            var result = await _validator.TestValidateAsync(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.FirstName)
                  .WithErrorCode(ErrorCodes.User.FirstNameInvalid);
        }

        [Test]
        public async Task Validate_WhenFirstNameExceedsFiftyCharacters_ShouldHaveValidationError()
        {
            // Arrange
            var request = new EditUserRequest { FirstName = new string('a', 51) };

            // Act
            var result = await _validator.TestValidateAsync(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.FirstName)
                  .WithErrorCode(ErrorCodes.User.FirstNameInvalid);
        }

        [Test]
        [Arguments("")]
        [Arguments("   ")]
        public async Task Validate_WhenLastNameIsEmptyOrWhitespace_ShouldHaveValidationError(string invalidLastName)
        {
            // Arrange
            var request = new EditUserRequest { LastName = invalidLastName };

            // Act
            var result = await _validator.TestValidateAsync(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.LastName)
                  .WithErrorCode(ErrorCodes.User.LastNameInvalid);
        }

        [Test]
        public async Task Validate_WhenLastNameIsShorterThanTwoCharacters_ShouldHaveValidationError()
        {
            // Arrange
            var request = new EditUserRequest { LastName = "K" };

            // Act
            var result = await _validator.TestValidateAsync(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.LastName)
                  .WithErrorCode(ErrorCodes.User.LastNameInvalid);
        }

        [Test]
        public async Task Validate_WhenLastNameExceedsFiftyCharacters_ShouldHaveValidationError()
        {
            // Arrange
            var request = new EditUserRequest { LastName = new string('k', 51) };

            // Act
            var result = await _validator.TestValidateAsync(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.LastName)
                  .WithErrorCode(ErrorCodes.User.LastNameInvalid);
        }

        [Test]
        public async Task Validate_WhenBioExceedsFiveHundredCharacters_ShouldHaveValidationError()
        {
            // Arrange
            var request = new EditUserRequest { Bio = new string('b', 501) };

            // Act
            var result = await _validator.TestValidateAsync(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Bio)
                  .WithErrorCode(ErrorCodes.User.BioInvalid);
        }

        [Test]
        public async Task Validate_WhenBioIsWithinLimitOrEmpty_ShouldNotHaveValidationError()
        {
            // Arrange
            var request = new EditUserRequest { Bio = "" };

            // Act
            var result = await _validator.TestValidateAsync(request);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.Bio);
        }
    }
}
