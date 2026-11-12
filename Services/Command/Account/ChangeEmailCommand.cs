namespace Services.Command.Account
{
    public record ChangeEmailCommand
    {
        public required string NewEmail { get; init; }
    }
}
