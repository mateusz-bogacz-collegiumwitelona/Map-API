using Domain.Enum;
using System;
using System.Collections.Generic;
using System.Text;

namespace Services.Response.Trip
{
    public sealed record TripDetailsSegmentResponse
    {
        public required int OrderIndex { get; init; }
        public required TransportTypeEnum TransportType { get; init; }
        public required decimal DistanceKm { get; init; }
        public required bool IsFallbackRoute { get; init; }
        public required TripPointResponse Origin { get; init; }
        public required TripPointResponse Destination { get; init; }
    }
}
