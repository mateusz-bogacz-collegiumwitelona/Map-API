using Domain.Enum;

namespace Services.Command.Trip
{
    public sealed record UpsertTripCommand(
        string Title,
        string? Description,
        DateTime StartDate,
        DateTime? EndDate,
        IReadOnlyList<TripSegmentCommand> Segments);
}