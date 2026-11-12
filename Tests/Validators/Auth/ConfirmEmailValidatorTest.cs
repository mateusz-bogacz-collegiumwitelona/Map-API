using Api.Request.Account;
using Api.Validators.Validators.Account;
using Domain.Const;
using FluentValidation.TestHelper;

namespace Tests.Validators.Auth
{
    public class ConfirmEmailValidatorTest
    {
        private readonly ConfirmEmailValidator _validator = new();

        private static ConfirmEmailRequest CreateValidRequest() => new()
        {
            Email = "jan.kowalski@example.com",
            Code = "123456"
        };

        #region Happy Path

        [Test]
        public async Task Validate_WhenEmailAndCodeAreValid_ShouldNotHaveAnyValidationErrors()
        {
            // Arrange
            var request = CreateValidRequest();

            // Act
            var result = await _validator.TestValidateAsync(request);

            // Assert
            await Assert.That(result.IsValid).IsTrue();
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
                  .WithErrorCode(ErrorCodes.EmailInvalid);
        }

        #endregion

        #region Code Validation

        [Test]
        [Arguments("")]
        [Arguments("   ")]
        public async Task Validate_WhenCodeIsEmptyOrWhitespace_ShouldHaveValidationError(string emptyCode)
        {
            // Arrange
            var request = CreateValidRequest() with { Code = emptyCode };

            // Act
            var result = await _validator.TestValidateAsync(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Code)
                  .WithErrorCode(ErrorCodes.CodeInvalid);
        }

        [Test]
        [Arguments("1")]
        [Arguments("12")]
        [Arguments("123")]
        [Arguments("1234")]
        [Arguments("12345")]
        [Arguments("1234567")]
        public async Task Validate_WhenCodeLengthIsNotExactlySixCharacters_ShouldHaveValidationError(string invalidLengthCode)
        {
            // Arrange
            var request = CreateValidRequest() with { Code = invalidLengthCode };

            // Act
            var result = await _validator.TestValidateAsync(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Code)
                  .WithErrorCode(ErrorCodes.CodeInvalid);
        }

        [Test]
        [Arguments("abcdef")]
        [Arguments("12345a")]
        [Arguments("12-456")]
        [Arguments(" 12345")]
        [Arguments("12345 ")]
        public async Task Validate_WhenCodeContainsNonDigitCharacters_ShouldHaveValidationError(string nonDigitCode)
        {
            // Arrange
            var request = CreateValidRequest() with { Code = nonDigitCode };

            // Act
            var result = await _validator.TestValidateAsync(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Code)
                  .WithErrorCode(ErrorCodes.CodeInvalid);
        }

        #endregion
    }
}
