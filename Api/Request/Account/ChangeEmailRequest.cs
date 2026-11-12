namespace Api.Request.Account
{
    public sealed record ChangeEmailRequest
    {
        public required string NewEmail { get; init; }
    }
}
