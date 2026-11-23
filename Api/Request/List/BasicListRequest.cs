namespace Api.Request.List
{
    public record BasicListRequest : SimpleListRequest
    {
        public string? SearchTerm { get; init; }
    }
}
