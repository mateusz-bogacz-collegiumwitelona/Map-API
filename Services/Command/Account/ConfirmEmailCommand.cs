namespace Services.Command.Auth
{
    public sealed record ConfirmEmailCommand
    {
        public required string Code { get; init; }
        public required string Email { get; init; }
    }
}
