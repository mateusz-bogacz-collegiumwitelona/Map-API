namespace Services.Command.Account
{
    public sealed record ResendVerificationCodeCommand
    {
        public required string Email { get; init; }
    }
}
