using System.Net.Mail;

namespace Email.Options
{
    public sealed class SmtpOptions
    {
        public const string SectionName = "Smtp";

        public string Host { get; set; } = null!;
        public int Port { get; set; }
        public string FromAddress { get; set; } = null!;
        public string FromName { get; set; } = null!;
        public string? Username { get; set; }
        public string? Password { get; set; }

        public bool IsValid()
            => !string.IsNullOrWhiteSpace(Host)
                && Port is > 0 and <= 65535
                && MailAddress.TryCreate(FromAddress, out _)
                && !string.IsNullOrWhiteSpace(FromName);
    }
}
