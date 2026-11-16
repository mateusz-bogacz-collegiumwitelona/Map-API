using Api.Request.Ban;
using Api.Validators.Validators.Ban;
using Domain.Const;
using FluentValidation.TestHelper;

namespace Tests.Validators.Ban
{
    public class ResolveReportValidatorTest
    {
        private readonly ResolveReportValidator _validator = new();

        #region Valid Scenarios

        [Test]
        public async Task Validate_WhenRejectedWithNullDuration_ShouldNotHaveAnyValidationErrors()
        {
            // Arrange
            var request = new ResolveReportRequest
            {
                Id = Guid.NewGuid(),
                IsBanned = false,
                Duration = null
            };

            // Act
            var result = await _validator.TestValidateAsync(request);

            // Assert
            await Assert.That(result.IsValid).IsTrue();
        }

        [Test]
        public async Task Validate_WhenBannedPermanentlyWithNullDuration_ShouldNotHaveAnyValidationErrors()
        {
            // Arrange
            var request = new ResolveReportRequest
            {
                Id = Guid.NewGuid(),
                IsBanned = true,
                Duration = null
            };

            // Act
            var result = await _validator.TestValidateAsync(request);

            // Assert
            await Assert.That(result.IsValid).IsTrue();
        }

        [Test]
        public async Task Validate_WhenBannedWithValidDuration_ShouldNotHaveAnyValidationErrors()
        {
            // Arrange
            var request = new ResolveReportRequest
            {
                Id = Guid.NewGuid(),
                IsBanned = true,
                Duration = TimeSpan.FromDays(7)
            };

            // Act
            var result = await _validator.TestValidateAsync(request);

            // Assert
            await Assert.That(result.IsValid).IsTrue();
        }

        [Test]
        public async Task Validate_WhenDurationIsAtBoundaries_ShouldNotHaveValidationError()
        {
            // Arrange
            var min = new ResolveReportRequest
            {
                Id = Guid.NewGuid(),
                IsBanned = true,
                Duration = TimeSpan.FromTicks(1)
            };
            var max = new ResolveReportRequest
            {
                Id = Guid.NewGuid(),
                IsBanned = true,
                Duration = TimeSpan.FromDays(365)
            };

            // Act
            var minResult = await _validator.TestValidateAsync(min);
            var maxResult = await _validator.TestValidateAsync(max);

            // Assert
            minResult.ShouldNotHaveValidationErrorFor(x => x.Duration);
            maxResult.ShouldNotHaveValidationErrorFor(x => x.Duration);
        }

        #endregion

        #region Duration Validation Tests

        [Test]
        [Arguments(0)]
        [Arguments(-1)]
        [Arguments(-60)]
        public async Task Validate_WhenDurationIsZeroOrNegative_ShouldHaveValidationError(int seconds)
        {
            // Arrange
            var request = new ResolveReportRequest
            {
                Id = Guid.NewGuid(),
                IsBanned = true,
                Duration = TimeSpan.FromSeconds(seconds)
            };

            // Act
            var result = await _validator.TestValidateAsync(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Duration)
                  .WithErrorCode(ErrorCodes.BanDurationInvalid);
        }

        [Test]
        public async Task Validate_WhenDurationExceedsMaxLimit_ShouldHaveValidationError()
        {
            // Arrange
            var request = new ResolveReportRequest
            {
                Id = Guid.NewGuid(),
                IsBanned = true,
                Duration = TimeSpan.FromDays(365).Add(TimeSpan.FromSeconds(1))
            };

            // Act
            var result = await _validator.TestValidateAsync(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Duration)
                  .WithErrorCode(ErrorCodes.BanDurationInvalid);
        }

        [Test]
        public async Task Validate_WhenNotBannedAndDurationIsProvided_ShouldHaveValidationError()
        {
            // Arrange
            var request = new ResolveReportRequest
            {
                Id = Guid.NewGuid(),
                IsBanned = false,
                Duration = TimeSpan.FromDays(1)
            };

            // Act
            var result = await _validator.TestValidateAsync(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Duration)
                  .WithErrorCode(ErrorCodes.BanDurationNotAllowed);
        }

        [Test]
        public async Task Validate_WhenNotBannedAndDurationIsInvalid_ShouldHaveBothDurationErrors()
        {
            // Arrange
            var request = new ResolveReportRequest
            {
                Id = Guid.NewGuid(),
                IsBanned = false,
                Duration = TimeSpan.Zero
            };

            // Act
            var result = await _validator.TestValidateAsync(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Duration)
                  .WithErrorCode(ErrorCodes.BanDurationInvalid);
            result.ShouldHaveValidationErrorFor(x => x.Duration)
                  .WithErrorCode(ErrorCodes.BanDurationNotAllowed);
        }

        [Test]
        public async Task Validate_WhenIdIsEmpty_ShouldHaveValidationError()
        {
            // Arrange
            var request = new ResolveReportRequest
            {
                Id = Guid.Empty,
                IsBanned = false,
                Duration = null
            };

            // Act
            var result = await _validator.TestValidateAsync(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Id)
                  .WithErrorCode(ErrorCodes.GuidInvalid);
        }

        #endregion
    }
}
