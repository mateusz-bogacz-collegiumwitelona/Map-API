namespace Services.Command.User
{
    public record ConfirmEmailChangeCommand
    {
        public required string Code { get; init; }
    }
}
