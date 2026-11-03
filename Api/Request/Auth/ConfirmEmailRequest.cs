namespace Api.Request.Auth
{
    public sealed record ConfirmEmailRequest
    {
        public required string Code { get; init; }
        public required string Email { get; init; }
    }
}
