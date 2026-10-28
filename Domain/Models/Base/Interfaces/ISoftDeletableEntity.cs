namespace Domain.Models.Base.Interfaces
{
    public interface ISoftDeletableEntity
    {
        bool IsDeleted { get; set; }
        DateTime? DeletedAt { get; set; }
    }
}
