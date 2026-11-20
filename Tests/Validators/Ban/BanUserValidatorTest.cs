using Api.Request.Ban;
using Api.Validators.Validators.Ban;
using Domain.Const;
using FluentValidation.TestHelper;

namespace Tests.Validators.Ban
{
    public class BanUserValidatorTest
    {
        private readonly BanUserValidator _validator = new();

        #region Valid requests
        [Test]
        public async Task Validate_WhenAllFieldsAreValidWithTemporaryBan_ShouldNotHaveAnyValidationErrors()
        {
            // Arrange
            var request = new BanUserRequest
            {
                UserId = Guid.NewGuid(),
                Reason = "Spam content",
                Duration = TimeSpan.FromDays(7)
            };

            // Act
            var result = await _validator.TestValidateAsync(request);

            // Assert
            await Assert.That(result.IsValid).IsTrue();
        }

        [Test]
        public async Task Validate_WhenAllFieldsAreValidWithPermanentBan_ShouldNotHaveAnyValidationErrors()
        {
            // Arrange
            var request = new BanUserRequest
            {
                UserId = Guid.NewGuid(),
                Reason = "Severe violation",
                Duration = null
            };

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
            var request = new BanUserRequest
            {
                UserId = Guid.Empty,
                Reason = "Spam content",
                Duration = null
            };

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
            var request = new BanUserRequest
            {
                UserId = Guid.NewGuid(),
                Reason = "Spam content",
                Duration = null
            };

            // Act
            var result = await _validator.TestValidateAsync(request);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.UserId);
            await Assert.That(result.IsValid).IsTrue();
        }
        #endregion

        #region Reason - empty
        [Test]
        public async Task Validate_WhenReasonIsNull_ShouldHaveValidationError()
        {
            // Arrange
            var request = new BanUserRequest
            {
                UserId = Guid.NewGuid(),
                Reason = null!,
                Duration = null
            };

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
            var request = new BanUserRequest
            {
                UserId = Guid.NewGuid(),
                Reason = string.Empty,
                Duration = null
            };

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
            var request = new BanUserRequest
            {
                UserId = Guid.NewGuid(),
                Reason = "          ",
                Duration = null
            };

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
            var request = new BanUserRequest
            {
                UserId = Guid.NewGuid(),
                Reason = new string('a', 4),
                Duration = null
            };

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
            var request = new BanUserRequest
            {
                UserId = Guid.NewGuid(),
                Reason = new string('a', 5),
                Duration = null
            };

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
            var request = new BanUserRequest
            {
                UserId = Guid.NewGuid(),
                Reason = new string('a', 500),
                Duration = null
            };

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
            var request = new BanUserRequest
            {
                UserId = Guid.NewGuid(),
                Reason = new string('a', 501),
                Duration = null
            };

            // Act
            var result = await _validator.TestValidateAsync(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Reason)
                  .WithErrorCode(ErrorCodes.ReportTooLong);
        }
        #endregion

        #region Duration
        [Test]
        [Arguments(0)]
        [Arguments(-1)]
        [Arguments(-60)]
        public async Task Validate_WhenDurationIsZeroOrNegative_ShouldHaveValidationError(int seconds)
        {
            // Arrange
            var request = new BanUserRequest
            {
                UserId = Guid.NewGuid(),
                Reason = "Spam content",
                Duration = TimeSpan.FromSeconds(seconds)
            };

            // Act
            var result = await _validator.TestValidateAsync(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Duration)
                  .WithErrorCode(ErrorCodes.BanDurationInvalid);
        }

        [Test]
        public async Task Validate_WhenDurationIsAtBoundaries_ShouldNotHaveValidationError()
        {
            // Arrange
            var min = new BanUserRequest
            {
                UserId = Guid.NewGuid(),
                Reason = "Spam content",
                Duration = TimeSpan.FromTicks(1)
            };
            var max = new BanUserRequest
            {
                UserId = Guid.NewGuid(),
                Reason = "Spam content",
                Duration = TimeSpan.FromDays(365)
            };

            // Act
            var minResult = await _validator.TestValidateAsync(min);
            var maxResult = await _validator.TestValidateAsync(max);

            // Assert
            minResult.ShouldNotHaveValidationErrorFor(x => x.Duration);
            maxResult.ShouldNotHaveValidationErrorFor(x => x.Duration);
        }

        [Test]
        public async Task Validate_WhenDurationExceedsMaxLimit_ShouldHaveValidationError()
        {
            // Arrange
            var request = new BanUserRequest
            {
                UserId = Guid.NewGuid(),
                Reason = "Spam content",
                Duration = TimeSpan.FromDays(365).Add(TimeSpan.FromSeconds(1))
            };

            // Act
            var result = await _validator.TestValidateAsync(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Duration)
                  .WithErrorCode(ErrorCodes.BanDurationInvalid);
        }
        #endregion

        #region Multiple errors
        [Test]
        public async Task Validate_WhenAllFieldsAreInvalid_ShouldHaveErrorsForAll()
        {
            // Arrange
            var request = new BanUserRequest
            {
                UserId = Guid.Empty,
                Reason = "abc",
                Duration = TimeSpan.Zero
            };

            // Act
            var result = await _validator.TestValidateAsync(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.UserId)
                  .WithErrorCode(ErrorCodes.GuidInvalid);
            result.ShouldHaveValidationErrorFor(x => x.Reason)
                  .WithErrorCode(ErrorCodes.ReportTooShort);
            result.ShouldHaveValidationErrorFor(x => x.Duration)
                  .WithErrorCode(ErrorCodes.BanDurationInvalid);
        }
        #endregion
    }
}