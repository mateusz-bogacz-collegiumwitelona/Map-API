using Services.Options;

namespace Tests.Auth;

public class JwtOptionsValidationTests
{
    private const string ValidSecret = "test-secret-key-with-at-least-32-characters";

    [Test]
    public async Task IsValid_WhenOptionsAreValid_ReturnsTrue()
    {
        // Arrange
        var options = CreateValidOptions();

        // Act
        var isValid = JwtOptionsValidation.IsValid(options);

        // Assert
        await Assert.That(isValid).IsTrue();
    }

    [Test]
    public async Task GetSigningKey_WhenCalledTwice_ReturnsSameCachedInstance()
    {
        // Arrange
        var options = CreateValidOptions();

        // Act
        var first = options.GetSigningKey();
        var second = options.GetSigningKey();

        // Assert
        await Assert.That(ReferenceEquals(first, second)).IsTrue();
    }

    [Test]
    public async Task GetSigningKey_WhenOptionsAreInvalid_ThrowsInvalidOperationException()
    {
        // Arrange
        var options = CreateValidOptions();
        options.SecretKey = "too-short";

        // Act & Assert
        await Assert.That(() => options.GetSigningKey()).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task IsValid_WhenSecretIsTooShort_ReturnsFalse()
    {
        // Arrange
        var options = CreateValidOptions();
        options.SecretKey = "too-short";

        // Act
        var isValid = JwtOptionsValidation.IsValid(options);

        // Assert
        await Assert.That(isValid).IsFalse();
    }

    [Test]
    public async Task IsValid_WhenExpirationIsNotPositive_ReturnsFalse()
    {
        // Arrange
        var options = CreateValidOptions();
        options.AccessTokenExpirationMinutes = 0;

        // Act
        var isValid = JwtOptionsValidation.IsValid(options);

        // Assert
        await Assert.That(isValid).IsFalse();
    }

    private static JwtOptions CreateValidOptions()
        => new()
        {
            SecretKey = ValidSecret,
            Issuer = "MAP-API-Tests",
            Audience = "MAP-API-Tests",
            AccessTokenExpirationMinutes = 15,
            RefreshTokenExpirationDays = 7
        };
}
