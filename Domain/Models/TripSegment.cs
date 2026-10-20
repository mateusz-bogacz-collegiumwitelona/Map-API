using Domain.Enum;
using Domain.Models.Base;
using NetTopologySuite.Geometries;

namespace Domain.Models
{
    public class TripSegment : BaseDeletableEntity
    {
        public Guid TripId { get; set; }
        public Trip Trip { get; set; } = null!;

        public int OrderIndex { get; set; }
        public TransportTypeEnum TransportType { get; set; }

        public Guid OriginPlaceId { get; set; }
        public Place OriginPlace { get; set; } = null!;

        public Guid DestinationPlaceId { get; set; }
        public Place DestinationPlace { get; set; } = null!;

        public decimal DistanceKm { get; set; }
        public Geometry Geometry { get; set; } = null!;
        public bool IsFallbackRoute { get; set; } = false;
    }
}
