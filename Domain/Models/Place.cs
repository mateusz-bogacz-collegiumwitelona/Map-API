using Domain.Models.Base;
using NetTopologySuite.Geometries;

namespace Domain.Models
{
    public class Place : BaseAuditableEntity
    {
        public string Name { get; set; } = null!;
        public Guid CountryId { get; set; }
        public Country Country { get; set; } = null!;
        public Point Location { get; set; } = null!;
    }
}
