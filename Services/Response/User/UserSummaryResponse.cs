namespace Services.Response.User
{
    public sealed record UserSummaryResponse
    {
        public required Guid UserId { get; init; }
        public required string FirstName { get; init; }
        public required string LastName { get; init; }
    }
}
