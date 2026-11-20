namespace Api.Request.List
{
    public record BasicListRequest
    {
        public int? PageNumber { get; init; }
        public int? PageSize { get; init; }
        public string? SortBy { get; init; }
        public bool SortDescending { get; init; } = true;
        public string? SearchTerm { get; init; }
    }
}
