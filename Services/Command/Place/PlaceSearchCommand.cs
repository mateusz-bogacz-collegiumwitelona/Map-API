namespace Services.Command.Place
{
    public sealed record PlaceSearchCommand
    {
        public required string Query { get; init; }
    }
}