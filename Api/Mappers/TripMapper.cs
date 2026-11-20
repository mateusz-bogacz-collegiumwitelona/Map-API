using Api.Mappers.Helper;
using Api.Request.Trip;
using Riok.Mapperly.Abstractions;
using Services.Command.Trip;

namespace Api.Mappers
{
    [Mapper]
    public partial class TripMapper : BaseMapper
    {
        [MapProperty(nameof(UpdateSegmentRouteRequest.DistanceKm), nameof(UpdateSegmentRouteCommand.DistanceKm), Use = nameof(RoundDistance))]
        public partial UpdateSegmentRouteCommand MapUpdateSegmentRoute(UpdateSegmentRouteRequest request);

        [UserMapping(Default = false)]
        private static decimal RoundDistance(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }
}
