namespace Email.Options
{
    public static class SmtpOptionsValidation
    {
        public const string ErrorMessage =
            "SMTP configuration is incomplete. Configure Smtp:Host, Smtp:Port (1-65535), Smtp:FromAddress " +
            "(a valid email address, Smtp__FromAddress in environment variables) and Smtp:FromName.";

        public static bool IsValid(SmtpOptions options)
            => options.IsValid();
    }
}
