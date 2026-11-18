using Domain.Enum;

namespace Api.Request.Trip
{
    public sealed record TripSegmentRequest
    {
        public required TripPointRequest Origin { get; init; }
        public required TripPointRequest Destination { get; init; }
        public required TransportTypeEnum TransportType { get; init; }
    }
}
