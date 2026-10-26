namespace Services.Command.User
{
    public record EditUserCommand
    {
        public string? FirstName { get; init; }
        public string? LastName { get; init; }
        public string? Bio { get; init; }
    }
}
