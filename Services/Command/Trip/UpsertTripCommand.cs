using Domain.Enum;

namespace Services.Command.Trip
{
    public sealed record UpsertTripCommand(
        string Title,
        string? Description,
        DateTime StartDate,
        DateTime? EndDate,
        IReadOnlyList<TripSegmentCommand> Segments);

    public sealed record TripSegmentCommand(
        TripPointCommand Origin,
        TripPointCommand Destination,
        TransportTypeEnum TransportType);

    public sealed record TripPointCommand(
        string Name,
        double Latitude,
        double Longitude);
}