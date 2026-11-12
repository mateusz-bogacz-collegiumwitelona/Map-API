using Email.Options;
using MailKit.Security;

namespace Tests.Email
{
    public class EmailServiceTest
    {
        #region GetSecureSocketOptions
        [Test]
        [Arguments(1025)]
        [Arguments(587)]
        public async Task GetSecureSocketOptions_WhenCredentialsAreMissing_UsesTlsOnlyIfOffered(int port)
        {
            // Arrange
            var options = CreateOptions(port, username: null);

            // Act
            var socketOptions = global::Email.EmailService.GetSecureSocketOptions(options);

            // Assert
            await Assert.That(socketOptions).IsEqualTo(SecureSocketOptions.Auto);
        }

        [Test]
        [Arguments(587)]
        [Arguments(25)]
        public async Task GetSecureSocketOptions_WhenCredentialsAreSet_RequiresStartTls(int port)
        {
            // Arrange
            var options = CreateOptions(port, username: "user");

            // Act
            var socketOptions = global::Email.EmailService.GetSecureSocketOptions(options);

            // Assert
            await Assert.That(socketOptions).IsEqualTo(SecureSocketOptions.StartTls);
        }

        [Test]
        public async Task GetSecureSocketOptions_WhenCredentialsAreSetOnPort465_UsesImplicitTls()
        {
            // Arrange
            var options = CreateOptions(465, username: "user");

            // Act
            var socketOptions = global::Email.EmailService.GetSecureSocketOptions(options);

            // Assert
            await Assert.That(socketOptions).IsEqualTo(SecureSocketOptions.SslOnConnect);
        }
        #endregion

        private static SmtpOptions CreateOptions(int port, string? username)
            => new()
            {
                Host = "smtp.example.com",
                Port = port,
                FromAddress = "no-reply@map.local",
                FromName = "Map",
                Username = username,
                Password = username is null ? null : "secret"
            };
    }
}
