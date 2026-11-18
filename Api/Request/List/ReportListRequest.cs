namespace Api.Request.List
{
    public sealed record ReportListRequest : BasicListRequest
    {
        public bool IsShowAll { get; init; } = false;
    }
}
