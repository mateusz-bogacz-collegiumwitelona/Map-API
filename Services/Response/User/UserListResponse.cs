namespace Services.Response.User
{
    public sealed record UserListResponse
    {
        public required Guid Id { get; init; }
        public required string FirstName { get; init; }
        public required string LastName { get; init; }
        public required string Role { get; init; }
        public bool IsBanned { get; init; }
        public DateTime? BannedUntil { get; init; }
        public required DateTime CreatedAt { get; init; }
        public required DateTime? UpdatedAt { get; init; }
    }
}
