namespace Api.Request.Ban
{
    public sealed record ReportUserRequest
    {
        public required Guid UserId { get; init; }
        public required string Reason { get; init; }
    }
}
