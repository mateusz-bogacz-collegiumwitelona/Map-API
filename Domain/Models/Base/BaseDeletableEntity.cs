using Domain.Models.Base.Interfaces;

namespace Domain.Models.Base
{
    public abstract class BaseDeletableEntity : BaseAuditableEntity, ISoftDeletableEntity
    {
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }
    }
}
