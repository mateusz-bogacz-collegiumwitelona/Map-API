namespace Services.Command.List
{
    public record ReportListCommand : BasicListCommand
    {
        public bool IsShowAll { get; init; } = false;
    }
}
