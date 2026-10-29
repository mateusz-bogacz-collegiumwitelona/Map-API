using Api.Request.Auth;
using Api.Validators.Validators.Auth;
using Domain.Const;
using FluentValidation.Results;

namespace Tests.Auth;

public class LogoutRequestValidatorTests
{
    private readonly LogoutRequestValidator _validator = new();

    [Test]
    public async Task Validate_WhenRefreshTokenIsNonEmpty_ReturnsValid()
    {
        // Arrange
        var request = new LogoutRequest
        {
            RefreshToken = "refresh-token"
        };

        // Act
        var result = _validator.Validate(request);

        // Assert
        await Assert.That(result.IsValid).IsTrue();
        await Assert.That(result.Errors).IsEmpty();
    }

    [Test]
    public async Task Validate_WhenRefreshTokenIsEmpty_ReturnsRefreshTokenValidationError()
    {
        // Arrange
        var request = new LogoutRequest
        {
            RefreshToken = string.Empty
        };

        // Act
        var result = _validator.Validate(request);

        // Assert
        await AssertSingleValidationError(result);
    }

    [Test]
    public async Task Validate_WhenRefreshTokenIsWhitespace_ReturnsRefreshTokenValidationError()
    {
        // Arrange
        var request = new LogoutRequest
        {
            RefreshToken = "   "
        };

        // Act
        var result = _validator.Validate(request);

        // Assert
        await AssertSingleValidationError(result);
    }

    private static async Task AssertSingleValidationError(ValidationResult result)
    {
        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.Errors.Count).IsEqualTo(1);

        var error = result.Errors[0];
        await Assert.That(error.PropertyName).IsEqualTo(nameof(LogoutRequest.RefreshToken));
        await Assert.That(error.ErrorCode).IsEqualTo(ErrorCodes.Validation.ValidationError);
    }
}
