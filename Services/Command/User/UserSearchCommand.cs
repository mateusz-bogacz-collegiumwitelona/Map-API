namespace Services.Command.User
{
    public sealed record UserSearchCommand
    {
        public string? Query { get; init; }
        public string? SortBy { get; init; }
        public int? PageNumber { get; init; }
        public int? PageSize { get; init; }
    }
}
