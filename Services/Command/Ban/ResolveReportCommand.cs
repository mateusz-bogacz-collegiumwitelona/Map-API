namespace Services.Command.Ban
{
    public sealed record ResolveReportCommand
    {
        public required Guid Id { get; init; }
        public required bool IsBanned { get; init; }
        public TimeSpan? Duration { get; init; }
        public string? Reason { get; init; }
    }
}
