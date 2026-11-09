using Domain.Enum;
using System.Text.Json.Serialization;

namespace Services.Response.Trip
{
    public record TripRouteSegmentResponse
    {
        public required int OrderIndex { get; init; }

        [JsonConverter(typeof(JsonStringEnumConverter<TransportTypeEnum>))]
        public required TransportTypeEnum TransportType { get; init; }

        public required decimal DistanceKm { get; init; }
        public required bool IsFallbackRoute { get; init; }
        public required string OriginName { get; init; }
        public required string DestinationName { get; init; }
        public required List<GeoCoordinate> Coordinates { get; init; }
    }
}
