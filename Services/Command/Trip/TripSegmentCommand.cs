using Domain.Enum;

namespace Services.Command.Trip
{
    public sealed record TripSegmentCommand
    {
        public required TripPointCommand Origin { get; init; }
        public required TripPointCommand Destination { get; init; }
        public required TransportTypeEnum TransportType { get; init; }
    }
}