namespace Domain.Models.Base
{
    public abstract class BaseDeletableEntity : BaseAuditableEntity
    {
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }
    }
}
