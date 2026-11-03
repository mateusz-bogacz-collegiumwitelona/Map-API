namespace Api.Request.Auth
{
    public sealed record RegisterRequest
    {
        public required string FirstName { get; init; }
        public required string LastName { get; init; }
        public required string Email { get; init; }
        public required string Password { get; init; }
        public required string ConfirmPassword { get; init; }
        public string? Bio { get; init; }
    }
}
