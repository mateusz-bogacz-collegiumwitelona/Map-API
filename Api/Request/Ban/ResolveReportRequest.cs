namespace Api.Request.Ban
{
    public sealed record ResolveReportRequest
    {
        public required Guid Id { get; init; }
        public required bool IsBanned { get; init; }
        public TimeSpan? Duration { get; init; }
    }
}
