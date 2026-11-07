namespace Api.Request.Account
{
    public sealed record ConfirmEmailRequest
    {
        public required string Code { get; init; }
        public required string Email { get; init; }
    }
}
