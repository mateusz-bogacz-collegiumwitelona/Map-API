using Api.Request.Trip;
using Domain.Enum;
using Api.Mappers.Helper;
using Riok.Mapperly.Abstractions;
using Services.Command.Trip;

namespace Api.Mappers
{
    [Mapper(
        EnumMappingStrategy = EnumMappingStrategy.ByName,
        EnumMappingIgnoreCase = true)]
    public partial class TripMapper
    {
        public partial UpsertTripCommand MapUpsertTrip(
            UpsertTripRequest request);

        public partial TripPointCommand MapTripPoint(
            TripPointRequest request);

        [MapProperty(
            nameof(TripSegmentRequest.TransportType),
            nameof(TripSegmentCommand.TransportType),
            Use = nameof(MapTransportType))]
        public partial TripSegmentCommand MapTripSegment(
            TripSegmentRequest request);

        [MapEnum(EnumMappingStrategy.ByName, IgnoreCase = true)]
        public partial TransportTypeEnum MapTransportType(
            string transportType);
    }
}
    [Mapper]
    public partial class TripMapper : BaseMapper
    {
        [MapProperty(nameof(UpdateSegmentRouteRequest.DistanceKm), nameof(UpdateSegmentRouteCommand.DistanceKm), Use = nameof(RoundDistance))]
        public partial UpdateSegmentRouteCommand MapUpdateSegmentRoute(UpdateSegmentRouteRequest request);

        [UserMapping(Default = false)]
        private static decimal RoundDistance(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }