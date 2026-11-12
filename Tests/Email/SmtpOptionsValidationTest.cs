using Email.Options;

namespace Tests.Email
{
    public class SmtpOptionsValidationTest
    {
        [Test]
        public async Task IsValid_WhenOptionsAreValid_ReturnsTrue()
        {
            // Arrange
            var options = CreateValidOptions();

            // Act
            var isValid = SmtpOptionsValidation.IsValid(options);

            // Assert
            await Assert.That(isValid).IsTrue();
        }

        [Test]
        public async Task IsValid_WhenCredentialsAreMissing_ReturnsTrue()
        {
            // Arrange
            var options = CreateValidOptions();
            options.Username = null;
            options.Password = null;

            // Act
            var isValid = SmtpOptionsValidation.IsValid(options);

            // Assert
            await Assert.That(isValid).IsTrue();
        }

        [Test]
        [Arguments("")]
        [Arguments("   ")]
        public async Task IsValid_WhenHostIsMissing_ReturnsFalse(string host)
        {
            // Arrange
            var options = CreateValidOptions();
            options.Host = host;

            // Act
            var isValid = SmtpOptionsValidation.IsValid(options);

            // Assert
            await Assert.That(isValid).IsFalse();
        }

        [Test]
        [Arguments(0)]
        [Arguments(-1)]
        [Arguments(65536)]
        public async Task IsValid_WhenPortIsOutOfRange_ReturnsFalse(int port)
        {
            // Arrange
            var options = CreateValidOptions();
            options.Port = port;

            // Act
            var isValid = SmtpOptionsValidation.IsValid(options);

            // Assert
            await Assert.That(isValid).IsFalse();
        }

        [Test]
        [Arguments("")]
        [Arguments("not-an-email")]
        public async Task IsValid_WhenFromAddressIsInvalid_ReturnsFalse(string fromAddress)
        {
            // Arrange
            var options = CreateValidOptions();
            options.FromAddress = fromAddress;

            // Act
            var isValid = SmtpOptionsValidation.IsValid(options);

            // Assert
            await Assert.That(isValid).IsFalse();
        }

        [Test]
        [Arguments("", 587, "no-reply@map.local", "Map", false)]
        [Arguments("smtp.example.com", 0, "no-reply@map.local", "Map", false)]
        [Arguments("smtp.example.com", 70000, "no-reply@map.local", "Map", false)]
        [Arguments("smtp.example.com", 587, "invalid-email", "Map", false)]
        [Arguments("smtp.example.com", 587, "no-reply@map.local", "", false)]
        [Arguments("smtp.example.com", 587, "no-reply@map.local", "Map", true)]
        public async Task SmtpOptions_IsValid_ValidatesRequiredFieldsCorrectly(
            string host, int port, string fromAddress, string fromName, bool expected)
        {
            // Arrange
            var options = new SmtpOptions
            {
                Host = host,
                Port = port,
                FromAddress = fromAddress,
                FromName = fromName
            };

            // Act
            var isValid = options.IsValid();

            // Assert
            await Assert.That(isValid).IsEqualTo(expected);
        }

        private static SmtpOptions CreateValidOptions()
            => new()
            {
                Host = "localhost",
                Port = 1025,
                FromAddress = "no-reply@map.local",
                FromName = "Map",
                Username = "user",
                Password = "secret"
            };
    }
}
