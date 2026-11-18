namespace Services.Command.List
{
    public sealed record UserListCommand
    {
        public int? PageNumber { get; init; }
        public int? PageSize { get; init; }
        public string? SortBy { get; init; }
        public bool SortDescending { get; init; } = true;
        public string? SearchTerm { get; init; }
        public bool IncludeBanned { get; init; } = false;
        public bool IncludeAdmins { get; init; } = false;
    }
}
