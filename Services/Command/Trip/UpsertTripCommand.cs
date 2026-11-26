namespace Services.Command.Trip
{
    public sealed record UpsertTripCommand
    {
        public required string Title { get; init; }
        public string? Description { get; init; }
        public required DateTime StartDate { get; init; }
        public DateTime? EndDate { get; init; }
        public required IReadOnlyList<TripSegmentCommand> Segments { get; init; }
    }
}