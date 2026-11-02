namespace Api.Request.User
{
    public sealed record ChangeEmailRequest
    {
        public required string NewEmail { get; init; }
    }
}
