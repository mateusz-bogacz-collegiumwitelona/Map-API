namespace Api.Request.List
{
    public sealed record UserListRequest : BasicListRequest
    {
        public bool IncludeBanned { get; init; } = false;
        public bool IncludeAdmins { get; init; } = false;
    }
}
