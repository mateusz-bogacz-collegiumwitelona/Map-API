using Domain.Models.Base.Interfaces;

namespace Domain.Models.Base
{
    public abstract class BaseAuditableEntity : BaseEntity, IAuditableEntity
    {
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}
