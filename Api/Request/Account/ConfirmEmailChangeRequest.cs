namespace Api.Request.Account
{
    public sealed record ConfirmEmailChangeRequest
    {
        public required string Code { get; init; }
    }
}
