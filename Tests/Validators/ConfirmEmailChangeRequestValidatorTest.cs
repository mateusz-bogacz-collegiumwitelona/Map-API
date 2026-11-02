using Api.Request.User;
using Api.Validators.User;
using Domain.Const;
using FluentValidation.TestHelper;

namespace Tests.Validators
{
    public class ConfirmEmailChangeRequestValidatorTest
    {
        private readonly ConfirmEmailChangeRequestValidator _validator = new();

        [Test]
        [Arguments("123456")]
        [Arguments("000000")]
        public async Task Validate_WhenCodeIsValid_ShouldNotHaveAnyValidationErrors(string code)
        {
            // Arrange
            var request = new ConfirmEmailChangeRequest { Code = code };

            // Act
            var result = await _validator.TestValidateAsync(request);

            // Assert
            await Assert.That(result.IsValid).IsTrue();
        }

        [Test]
        [Arguments("")]
        [Arguments("   ")]
        [Arguments("12345")]
        [Arguments("1234567")]
        [Arguments("12a456")]
        [Arguments("12 456")]
        [Arguments(" 123456 ")]
        [Arguments("١٢٣٤٥٦")]
        public async Task Validate_WhenCodeIsInvalid_ShouldHaveCodeInvalidError(string code)
        {
            // Arrange
            var request = new ConfirmEmailChangeRequest { Code = code };

            // Act
            var result = await _validator.TestValidateAsync(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Code)
                .WithErrorCode(ErrorCodes.Verification.CodeInvalid);
            await Assert.That(result.IsValid).IsFalse();
        }

        [Test]
        public async Task Validate_WhenCodeIsNull_ShouldHaveCodeInvalidError()
        {
            // Arrange
            var request = new ConfirmEmailChangeRequest { Code = null! };

            // Act
            var result = await _validator.TestValidateAsync(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Code)
                .WithErrorCode(ErrorCodes.Verification.CodeInvalid);
            await Assert.That(result.IsValid).IsFalse();
        }
    }
}
