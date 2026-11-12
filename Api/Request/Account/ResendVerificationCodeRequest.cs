namespace Api.Request.Account
{
    public sealed record ResendVerificationCodeRequest
    {
        public required string Email { get; init; }
    }
}
