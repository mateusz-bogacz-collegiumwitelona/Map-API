using Api.Request.User;
using Api.Validators.User;
using Domain.Const;
using FluentValidation.TestHelper;

namespace Tests.Validators
{
    public class ChangeEmailRequestValidatorTest
    {
        private readonly ChangeEmailRequestValidator _validator = new();

        [Test]
        [Arguments("new@example.com")]
        [Arguments("  new@example.com  ")]
        [Arguments("first.last+tag@sub.example.pl")]
        public async Task Validate_WhenEmailIsValid_ShouldNotHaveAnyValidationErrors(string email)
        {
            // Arrange
            var request = new ChangeEmailRequest { NewEmail = email };

            // Act
            var result = await _validator.TestValidateAsync(request);

            // Assert
            await Assert.That(result.IsValid).IsTrue();
        }

        [Test]
        [Arguments("")]
        [Arguments("   ")]
        [Arguments("not-an-email")]
        [Arguments("missing-domain@")]
        [Arguments("@missing-local.com")]
        [Arguments("two@@example.com")]
        public async Task Validate_WhenEmailIsInvalid_ShouldHaveEmailInvalidError(string email)
        {
            // Arrange
            var request = new ChangeEmailRequest { NewEmail = email };

            // Act
            var result = await _validator.TestValidateAsync(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.NewEmail)
                .WithErrorCode(ErrorCodes.EmailInvalid);
            await Assert.That(result.IsValid).IsFalse();
        }

        [Test]
        public async Task Validate_WhenEmailIsNull_ShouldHaveEmailInvalidError()
        {
            // Arrange
            var request = new ChangeEmailRequest { NewEmail = null! };

            // Act
            var result = await _validator.TestValidateAsync(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.NewEmail)
                .WithErrorCode(ErrorCodes.EmailInvalid);
            await Assert.That(result.IsValid).IsFalse();
        }

        [Test]
        public async Task Validate_WhenEmailIsLongerThan256Characters_ShouldHaveEmailInvalidError()
        {
            // Arrange
            var request = new ChangeEmailRequest { NewEmail = new string('a', 250) + "@example.com" };

            // Act
            var result = await _validator.TestValidateAsync(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.NewEmail)
                .WithErrorCode(ErrorCodes.EmailInvalid);
            await Assert.That(result.IsValid).IsFalse();
        }
    }
}
