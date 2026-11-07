using Api.Request.Account;
using Api.Validators.Validators.Account;
using Domain.Const;
using FluentValidation.TestHelper;

namespace Tests.Validators.Auth
{
    public class RegisterValidatorTest
    {
        private readonly RegisterValidator _validator = new();

        private static RegisterRequest CreateValidRequest() => new()
        {
            FirstName = "Jan",
            LastName = "Kowalski",
            Email = "jan.kowalski@example.com",
            Password = "Password123!",
            ConfirmPassword = "Password123!",
            Bio = "Przykładowy opis"
        };

        #region Happy Path

        [Test]
        public async Task Validate_WhenAllFieldsAreValid_ShouldNotHaveAnyValidationErrors()
        {
            // Arrange
            var request = CreateValidRequest();

            // Act
            var result = await _validator.TestValidateAsync(request);

            // Assert
            await Assert.That(result.IsValid).IsTrue();
        }

        [Test]
        public async Task Validate_WhenBioIsNull_ShouldBeValid()
        {
            // Arrange
            var request = CreateValidRequest() with { Bio = null };

            // Act
            var result = await _validator.TestValidateAsync(request);

            // Assert
            await Assert.That(result.IsValid).IsTrue();
        }

        #endregion

        #region FirstName Validation

        [Test]
        [Arguments("")]
        [Arguments("   ")]
        public async Task Validate_WhenFirstNameIsEmptyOrWhitespace_ShouldHaveValidationError(string invalidFirstName)
        {
            // Arrange
            var request = CreateValidRequest() with { FirstName = invalidFirstName };

            // Act
            var result = await _validator.TestValidateAsync(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.FirstName)
                  .WithErrorCode(ErrorCodes.FirstNameInvalid);
        }

        [Test]
        public async Task Validate_WhenFirstNameIsShorterThanTwoCharacters_ShouldHaveValidationError()
        {
            // Arrange
            var request = CreateValidRequest() with { FirstName = "A" };

            // Act
            var result = await _validator.TestValidateAsync(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.FirstName)
                  .WithErrorCode(ErrorCodes.FirstNameInvalid);
        }

        [Test]
        public async Task Validate_WhenFirstNameExceedsFiftyCharacters_ShouldHaveValidationError()
        {
            // Arrange
            var request = CreateValidRequest() with { FirstName = new string('a', 51) };

            // Act
            var result = await _validator.TestValidateAsync(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.FirstName)
                  .WithErrorCode(ErrorCodes.FirstNameInvalid);
        }

        #endregion

        #region LastName Validation

        [Test]
        [Arguments("")]
        [Arguments("   ")]
        public async Task Validate_WhenLastNameIsEmptyOrWhitespace_ShouldHaveValidationError(string invalidLastName)
        {
            // Arrange
            var request = CreateValidRequest() with { LastName = invalidLastName };

            // Act
            var result = await _validator.TestValidateAsync(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.LastName)
                  .WithErrorCode(ErrorCodes.LastNameInvalid);
        }

        [Test]
        public async Task Validate_WhenLastNameIsShorterThanTwoCharacters_ShouldHaveValidationError()
        {
            // Arrange
            var request = CreateValidRequest() with { LastName = "K" };

            // Act
            var result = await _validator.TestValidateAsync(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.LastName)
                  .WithErrorCode(ErrorCodes.LastNameInvalid);
        }

        [Test]
        public async Task Validate_WhenLastNameExceedsFiftyCharacters_ShouldHaveValidationError()
        {
            // Arrange
            var request = CreateValidRequest() with { LastName = new string('k', 51) };

            // Act
            var result = await _validator.TestValidateAsync(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.LastName)
                  .WithErrorCode(ErrorCodes.LastNameInvalid);
        }

        #endregion

        #region Email Validation

        [Test]
        [Arguments("")]
        [Arguments("   ")]
        [Arguments("not-an-email")]
        [Arguments("missingatsign.com")]
        [Arguments("@missingusername.com")]
        public async Task Validate_WhenEmailIsInvalid_ShouldHaveValidationError(string invalidEmail)
        {
            // Arrange
            var request = CreateValidRequest() with { Email = invalidEmail };

            // Act
            var result = await _validator.TestValidateAsync(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Email)
                  .WithErrorCode(ErrorCodes.ValidationError);
        }

        #endregion

        #region Password & ConfirmPassword Validation

        [Test]
        [Arguments("")]
        [Arguments("   ")]
        public async Task Validate_WhenPasswordIsEmpty_ShouldHaveValidationError(string invalidPassword)
        {
            // Arrange
            var request = CreateValidRequest() with
            {
                Password = invalidPassword,
                ConfirmPassword = invalidPassword
            };

            // Act
            var result = await _validator.TestValidateAsync(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Password)
                  .WithErrorCode(ErrorCodes.ValidationError);
        }

        [Test]
        public async Task Validate_WhenConfirmPasswordDoesNotMatchPassword_ShouldHaveValidationError()
        {
            // Arrange
            var request = CreateValidRequest() with
            {
                Password = "Password123!",
                ConfirmPassword = "DifferentPassword123!"
            };

            // Act
            var result = await _validator.TestValidateAsync(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.ConfirmPassword)
                  .WithErrorCode(ErrorCodes.ValidationError);
            result.ShouldNotHaveValidationErrorFor(x => x.Password);
        }

        [Test]
        public async Task Validate_WhenConfirmPasswordIsEmpty_ShouldHaveValidationError()
        {
            // Arrange
            var request = CreateValidRequest() with { ConfirmPassword = "" };

            // Act
            var result = await _validator.TestValidateAsync(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.ConfirmPassword)
                  .WithErrorCode(ErrorCodes.ValidationError);
        }

        #endregion

        #region Bio Validation

        [Test]
        public async Task Validate_WhenBioExceedsFiveHundredCharacters_ShouldHaveValidationError()
        {
            // Arrange
            var request = CreateValidRequest() with { Bio = new string('b', 501) };

            // Act
            var result = await _validator.TestValidateAsync(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Bio)
                  .WithErrorCode(ErrorCodes.BioInvalid);
        }

        [Test]
        public async Task Validate_WhenBioIsWithinLimitOrEmpty_ShouldNotHaveValidationError()
        {
            // Arrange
            var request = CreateValidRequest() with { Bio = "" };

            // Act
            var result = await _validator.TestValidateAsync(request);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.Bio);
        }

        #endregion
    }
}