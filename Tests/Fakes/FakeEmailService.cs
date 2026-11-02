using Services.Interfaces;

namespace Tests.Fakes
{
    internal sealed record SentEmail(string To, string Type, string? Code = null, string? NewEmail = null);

    internal class FakeEmailService : IEmailService
    {
        public const string EmailChangeCodeType = "EmailChangeCode";
        public const string EmailChangedNotificationType = "EmailChangedNotification";

        private readonly Lock _lock = new();

        public List<SentEmail> SentEmails { get; } = new();
        public bool ThrowOnSend { get; set; }

        public Task SendEmailChangeCodeAsync(string toEmail, string firstName, string code, int validForMinutes, CancellationToken cancellationToken = default)
        {
            ThrowIfConfigured();
            Record(new SentEmail(toEmail, EmailChangeCodeType, Code: code));
            return Task.CompletedTask;
        }

        public Task SendEmailChangedNotificationAsync(string toEmail, string firstName, string newEmail, CancellationToken cancellationToken = default)
        {
            ThrowIfConfigured();
            Record(new SentEmail(toEmail, EmailChangedNotificationType, NewEmail: newEmail));
            return Task.CompletedTask;
        }

        // Concurrency tests send from several scopes at once.
        private void Record(SentEmail email)
        {
            lock (_lock)
            {
                SentEmails.Add(email);
            }
        }

        private void ThrowIfConfigured()
        {
            if (ThrowOnSend)
            {
                throw new InvalidOperationException("SMTP server is unavailable.");
            }
        }
    }
}
