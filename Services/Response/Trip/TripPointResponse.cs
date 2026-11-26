using System;
using System.Collections.Generic;
using System.Text;

namespace Services.Response.Trip
{
    public sealed record TripPointResponse
    {
        public required string Name { get; init; }
        public required Guid PlaceId { get; init; }
        public required double Latitude { get; init; }
        public required double Longitude { get; init; }
    }
}
