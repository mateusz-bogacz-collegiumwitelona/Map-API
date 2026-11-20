using Api.Request.Ban;
using Api.Validators.Validators.Ban;
using Domain.Const;
using FluentValidation.TestHelper;

namespace Tests.Validators.Ban
{
    public class ReportUserValidatorTest
    {
        private readonly ReportUserValidator _validator = new();

        private static ReportUserRequest CreateValidRequest()
            => new ReportUserRequest { UserId = Guid.NewGuid(), Reason = "Spam content" };

        #region Valid requests
        [Test]
        public async Task Validate_WhenAllFieldsAreValid_ShouldNotHaveAnyValidationErrors()
        {
            // Arrange
            var request = new ReportUserRequest { UserId = Guid.NewGuid(), Reason = "Spam content" };

            // Act
            var result = await _validator.TestValidateAsync(request);

            // Assert
            await Assert.That(result.IsValid).IsTrue();
        }
        #endregion

        #region UserId
        [Test]
        public async Task Validate_WhenUserIdIsEmptyGuid_ShouldHaveValidationError()
        {
            // Arrange
            var request = new ReportUserRequest { UserId = Guid.Empty, Reason = "Spam content" };

            // Act
            var result = await _validator.TestValidateAsync(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.UserId)
                  .WithErrorCode(ErrorCodes.GuidInvalid);
        }

        [Test]
        public async Task Validate_WhenUserIdIsValid_ShouldNotHaveValidationError()
        {
            // Arrange
            var request = new ReportUserRequest { UserId = Guid.NewGuid(), Reason = "Spam content" };

            // Act
            var result = await _validator.TestValidateAsync(request);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.UserId);
            await Assert.That(result.IsValid).IsTrue();
        }

        [Test]
        public async Task Validate_WhenUserIdIsEmptyGuid_ShouldNotHaveValidationErrorForReason()
        {
            // Arrange
            var request = new ReportUserRequest { UserId = Guid.Empty, Reason = "Spam content" };

            // Act
            var result = await _validator.TestValidateAsync(request);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.Reason);
            await Assert.That(result.IsValid).IsFalse();
        }
        #endregion

        #region Reason - empty
        [Test]
        public async Task Validate_WhenReasonIsNull_ShouldHaveValidationError()
        {
            // Arrange
            var request = new ReportUserRequest { UserId = Guid.NewGuid(), Reason = null! };

            // Act
            var result = await _validator.TestValidateAsync(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Reason)
                  .WithErrorCode(ErrorCodes.ReportEmpty);
        }

        [Test]
        public async Task Validate_WhenReasonIsEmptyString_ShouldHaveValidationError()
        {
            // Arrange
            var request = new ReportUserRequest { UserId = Guid.NewGuid(), Reason = string.Empty };

            // Act
            var result = await _validator.TestValidateAsync(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Reason)
                  .WithErrorCode(ErrorCodes.ReportEmpty);
        }

        [Test]
        public async Task Validate_WhenReasonIsOnlyWhitespace_ShouldHaveValidationError()
        {
            // Arrange
            var request = new ReportUserRequest { UserId = Guid.NewGuid(), Reason = "          " };

            // Act
            var result = await _validator.TestValidateAsync(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Reason)
                  .WithErrorCode(ErrorCodes.ReportEmpty);
        }
        #endregion

        #region Reason - length
        [Test]
        public async Task Validate_WhenReasonIsBelowMinimumLength_ShouldHaveValidationError()
        {
            // Arrange
            var request = new ReportUserRequest { UserId = Guid.NewGuid(), Reason = new string('a', 4) };

            // Act
            var result = await _validator.TestValidateAsync(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Reason)
                  .WithErrorCode(ErrorCodes.ReportTooShort);
        }

        [Test]
        public async Task Validate_WhenReasonIsAtMinimumLength_ShouldNotHaveValidationError()
        {
            // Arrange
            var request = new ReportUserRequest { UserId = Guid.NewGuid(), Reason = new string('a', 5) };

            // Act
            var result = await _validator.TestValidateAsync(request);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.Reason);
            await Assert.That(result.IsValid).IsTrue();
        }

        [Test]
        public async Task Validate_WhenReasonIsAtMaximumLength_ShouldNotHaveValidationError()
        {
            // Arrange
            var request = new ReportUserRequest { UserId = Guid.NewGuid(), Reason = new string('a', 500) };

            // Act
            var result = await _validator.TestValidateAsync(request);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.Reason);
            await Assert.That(result.IsValid).IsTrue();
        }

        [Test]
        public async Task Validate_WhenReasonIsAboveMaximumLength_ShouldHaveValidationError()
        {
            // Arrange
            var request = new ReportUserRequest { UserId = Guid.NewGuid(), Reason = new string('a', 501) };

            // Act
            var result = await _validator.TestValidateAsync(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Reason)
                  .WithErrorCode(ErrorCodes.ReportTooLong);
        }
        #endregion

        #region Multiple errors
        [Test]
        public async Task Validate_WhenUserIdAndReasonAreInvalid_ShouldHaveErrorsForBoth()
        {
            // Arrange
            var request = new ReportUserRequest { UserId = Guid.Empty, Reason = "abc" };

            // Act
            var result = await _validator.TestValidateAsync(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.UserId)
                  .WithErrorCode(ErrorCodes.GuidInvalid);
            result.ShouldHaveValidationErrorFor(x => x.Reason)
                  .WithErrorCode(ErrorCodes.ReportTooShort);
        }
        #endregion
    }
}
