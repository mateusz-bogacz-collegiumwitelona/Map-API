namespace Services.Command.Auth
{
    public sealed record RegisterCommand
    {
        public required string FirstName { get; init; }
        public required string LastName { get; init; }
        public required string Email { get; init; }
        public required string Password { get; init; }
        public string? Bio { get; init; }
        public bool IsPublic { get; init; }
    }
}
