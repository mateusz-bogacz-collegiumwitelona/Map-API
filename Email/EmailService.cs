using Email.Options;
using Email.Templates;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using Services.Interfaces;

namespace Email
{
    public class EmailService : IEmailService
    {
        private const int ImplicitTlsPort = 465;

        private readonly SmtpOptions _options;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IOptions<SmtpOptions> options, ILogger<EmailService> logger)
        {
            _options = options.Value;
            _logger = logger;
        }

        public Task SendEmailChangeCodeAsync(string toEmail, string firstName, string code, int validForMinutes, CancellationToken cancellationToken = default)
            => SendAsync(toEmail, EmailTemplates.EmailChangeCode(firstName, code, validForMinutes), cancellationToken);

        public Task SendEmailChangedNotificationAsync(string toEmail, string firstName, string newEmail, CancellationToken cancellationToken = default)
            => SendAsync(toEmail, EmailTemplates.EmailChangedNotification(firstName, newEmail), cancellationToken);

        private async Task SendAsync(string toEmail, EmailContent content, CancellationToken cancellationToken)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_options.FromName, _options.FromAddress));
            message.To.Add(MailboxAddress.Parse(toEmail));
            message.Subject = content.Subject;
            message.Body = new BodyBuilder
            {
                HtmlBody = content.HtmlBody,
                TextBody = content.TextBody
            }.ToMessageBody();

            using var client = new SmtpClient();

            await client.ConnectAsync(_options.Host, _options.Port, GetSecureSocketOptions(_options), cancellationToken);

            if (!string.IsNullOrWhiteSpace(_options.Username))
            {
                await client.AuthenticateAsync(_options.Username, _options.Password ?? string.Empty, cancellationToken);
            }

            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);

            _logger.LogInformation("Email '{Subject}' sent successfully.", content.Subject);
        }

        // With credentials configured TLS is required, so a server without STARTTLS fails instead of receiving the password in plain text.
        // Without credentials (local MailPit) encryption is used only when the server offers it.
        public static SecureSocketOptions GetSecureSocketOptions(SmtpOptions options)
        {
            if (string.IsNullOrWhiteSpace(options.Username))
            {
                return SecureSocketOptions.Auto;
            }

            return options.Port == ImplicitTlsPort ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
        }
    }
}
