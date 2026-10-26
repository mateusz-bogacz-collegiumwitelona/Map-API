namespace Services.Response.User
{
    public record UserResponse
    {
        public required Guid UserId { get; init; }
        public required string FirstName { get; init; }
        public required string LastName { get; init; }
        public string? Bio { get; init; }
        public bool IsPublic { get; init; }
    }
}
