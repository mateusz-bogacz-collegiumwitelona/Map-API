namespace Api.Request.Ban
{
    public sealed record BanUserRequest
    {
        public required Guid UserId { get; init; }
        public required string Reason { get; init; }
        public TimeSpan? Duration { get; init; }
    }
}
