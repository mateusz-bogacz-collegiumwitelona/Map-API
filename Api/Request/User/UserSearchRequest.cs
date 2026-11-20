namespace Api.Request.User
{
    public sealed record UserSearchRequest
    {
        public string? Q { get; init; }
        public string? Sort { get; init; }
        public int? Page { get; init; }
        public int? Limit { get; init; }
    }
}
