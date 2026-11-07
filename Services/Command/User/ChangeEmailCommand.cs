namespace Services.Command.User
{
    public record ChangeEmailCommand
    {
        public required string NewEmail { get; init; }
    }
}
