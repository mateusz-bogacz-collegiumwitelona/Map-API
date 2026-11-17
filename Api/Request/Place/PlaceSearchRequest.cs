namespace Api.Request.Place
{
    public sealed record PlaceSearchRequest
    {
        public string? Query { get; init; }
    }
}