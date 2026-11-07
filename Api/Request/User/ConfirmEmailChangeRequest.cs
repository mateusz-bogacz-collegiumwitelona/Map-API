namespace Api.Request.User
{
    public sealed record ConfirmEmailChangeRequest
    {
        public required string Code { get; init; }
    }
}
