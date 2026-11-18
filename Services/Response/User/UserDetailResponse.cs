namespace Services.Response.User
{
    public sealed record UserDetailResponse
    {
        public required Guid Id { get; init; }
        public required string FirstName { get; init; }
        public required string LastName { get; init; }
        public required string Email { get; init; }
        public required string Role { get; init; }
        public bool IsPublic { get; init; }
        public bool IsBanned { get; init; }
    }
}
