namespace Services.Command.Ban
{
    public sealed record ReportUserCommand
    {
        public required Guid UserId { get; init; }
        public required string Reason { get; init; }
    }
}
