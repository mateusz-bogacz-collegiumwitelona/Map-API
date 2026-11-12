using Api.Request.Account;
using Api.Validators.Validators.Account;
using Domain.Const;
using FluentValidation.TestHelper;

namespace Tests.Validators.Account
{
    public class ResendVerificationCodeValidatorTest
    {
        private readonly ResendVerificationCodeValidator _validator = new();

        [Test]
        [Arguments("user@example.com")]
        [Arguments("test.user+tag@domain.co.uk")]
        [Arguments("first.last@sub.domain.org")]
        public async Task Validate_WhenEmailIsValid_ShouldNotHaveAnyValidationErrors(string email)
        {
            // Arrange
            var request = new ResendVerificationCodeRequest { Email = email };

            // Act
            var result = await _validator.TestValidateAsync(request);

            // Assert
            await Assert.That(result.IsValid).IsTrue();
        }

        [Test]
        [Arguments("")]
        [Arguments("   ")]
        [Arguments("invalid-email")]
        [Arguments("@missingusername.com")]
        [Arguments("missingdomain@")]
        [Arguments("plainaddress")]
        public async Task Validate_WhenEmailIsInvalid_ShouldHaveEmailInvalidError(string email)
        {
            // Arrange
            var request = new ResendVerificationCodeRequest { Email = email };

            // Act
            var result = await _validator.TestValidateAsync(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Email)
                .WithErrorCode(ErrorCodes.EmailInvalid);
            await Assert.That(result.IsValid).IsFalse();
        }

        [Test]
        public async Task Validate_WhenEmailIsNull_ShouldHaveEmailInvalidError()
        {
            // Arrange
            var request = new ResendVerificationCodeRequest { Email = null! };

            // Act
            var result = await _validator.TestValidateAsync(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Email)
                .WithErrorCode(ErrorCodes.EmailInvalid);
            await Assert.That(result.IsValid).IsFalse();
        }

        [Test]
        public async Task Validate_WhenEmailExceedsMaximumLength_ShouldHaveEmailInvalidError()
        {
            // Arrange
            var longEmail = new string('a', 245) + "@example.com"; // 257 znaków (> 256)
            var request = new ResendVerificationCodeRequest { Email = longEmail };

            // Act
            var result = await _validator.TestValidateAsync(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Email)
                .WithErrorCode(ErrorCodes.EmailInvalid);
            await Assert.That(result.IsValid).IsFalse();
        }
    }
}
