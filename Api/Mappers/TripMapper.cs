using Api.Request.Trip;
using Domain.Enum;
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