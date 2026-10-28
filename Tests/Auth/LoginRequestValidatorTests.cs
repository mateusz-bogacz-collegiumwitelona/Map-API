using Api.Request.Auth;
using Api.Validators.Validators.Auth;
using Domain.Const;
using FluentValidation.Results;

namespace Tests.Auth;

public class LoginRequestValidatorTests
{
    private readonly LoginRequestValidator _validator = new();

    [Test]
    public async Task Validate_WhenEmailAndPasswordAreValid_ReturnsValid()
    {
        // Arrange
        var request = new LoginRequest
        {
            Email = "user@example.com",
            Password = "Password123!"
        };

        // Act
        var result = _validator.Validate(request);

        // Assert
        await Assert.That(result.IsValid).IsTrue();
        await Assert.That(result.Errors).IsEmpty();
    }

    [Test]
    public async Task Validate_WhenEmailIsEmpty_ReturnsEmailValidationErrors()
    {
        // Arrange
        var request = new LoginRequest
        {
            Email = string.Empty,
            Password = "Password123!"
        };

        // Act
        var result = _validator.Validate(request);

        // Assert
        await AssertEmailValidationErrors(result, expectedCount: 2);
    }

    [Test]
    public async Task Validate_WhenEmailIsWhitespace_ReturnsEmailValidationErrors()
    {
        // Arrange
        var request = new LoginRequest
        {
            Email = "   ",
            Password = "Password123!"
        };

        // Act
        var result = _validator.Validate(request);

        // Assert
        await AssertEmailValidationErrors(result, expectedCount: 2);
    }

    [Test]
    public async Task Validate_WhenEmailFormatIsInvalid_ReturnsEmailValidationError()
    {
        // Arrange
        var request = new LoginRequest
        {
            Email = "not-an-email",
            Password = "Password123!"
        };

        // Act
        var result = _validator.Validate(request);

        // Assert
        await AssertEmailValidationErrors(result, expectedCount: 1);
    }

    [Test]
    public async Task Validate_WhenPasswordIsEmpty_ReturnsPasswordValidationError()
    {
        // Arrange
        var request = new LoginRequest
        {
            Email = "user@example.com",
            Password = string.Empty
        };

        // Act
        var result = _validator.Validate(request);

        // Assert
        await AssertSingleValidationError(result, nameof(LoginRequest.Password));
    }

    [Test]
    public async Task Validate_WhenPasswordIsWhitespace_ReturnsPasswordValidationError()
    {
        // Arrange
        var request = new LoginRequest
        {
            Email = "user@example.com",
            Password = "   "
        };

        // Act
        var result = _validator.Validate(request);

        // Assert
        await AssertSingleValidationError(result, nameof(LoginRequest.Password));
    }

    [Test]
    public async Task Validate_WhenEmailAndPasswordAreEmpty_ReturnsErrorsForBothProperties()
    {
        // Arrange
        var request = new LoginRequest
        {
            Email = string.Empty,
            Password = string.Empty
        };

        // Act
        var result = _validator.Validate(request);

        // Assert
        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.Errors.Count).IsEqualTo(3);
        await Assert.That(result.Errors.Count(error => error.PropertyName == nameof(LoginRequest.Email))).IsEqualTo(2);
        await Assert.That(result.Errors.Count(error => error.PropertyName == nameof(LoginRequest.Password))).IsEqualTo(1);
        await Assert.That(result.Errors.All(error => error.ErrorCode == ErrorCodes.Validation.ValidationError)).IsTrue();
    }

    private static async Task AssertEmailValidationErrors(ValidationResult result, int expectedCount)
    {
        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.Errors.Count).IsEqualTo(expectedCount);
        await Assert.That(result.Errors.All(error => error.PropertyName == nameof(LoginRequest.Email))).IsTrue();
        await Assert.That(result.Errors.All(error => error.ErrorCode == ErrorCodes.Validation.ValidationError)).IsTrue();
    }

    private static async Task AssertSingleValidationError(ValidationResult result, string propertyName)
    {
        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.Errors.Count).IsEqualTo(1);

        var error = result.Errors[0];
        await Assert.That(error.PropertyName).IsEqualTo(propertyName);
        await Assert.That(error.ErrorCode).IsEqualTo(ErrorCodes.Validation.ValidationError);
    }
}
