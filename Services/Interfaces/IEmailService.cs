namespace Services.Interfaces
{
    public interface IEmailService
    {
        Task SendEmailChangeCodeAsync(string toEmail, string firstName, string code, int validForMinutes, CancellationToken cancellationToken = default);
        Task SendEmailChangedNotificationAsync(string toEmail, string firstName, string newEmail, CancellationToken cancellationToken = default);
    }
}
