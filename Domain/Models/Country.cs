using Domain.Models.Base;
using NetTopologySuite.Geometries;

namespace Domain.Models
{
    public class Country : BaseAuditableEntity
    {
        public string Name { get; set; } = null!;
        public string Code { get; set; } = null!;
        public MultiPolygon Boundary { get; set; } = null!;
    }
}
