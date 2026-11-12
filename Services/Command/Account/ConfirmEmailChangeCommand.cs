namespace Services.Command.Account
{
    public record ConfirmEmailChangeCommand
    {
        public required string Code { get; init; }
    }
}
