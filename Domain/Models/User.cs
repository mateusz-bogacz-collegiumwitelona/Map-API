using Domain.Models.Base.Interfaces;
using Microsoft.AspNetCore.Identity;

namespace Domain.Models
{
    public class User : IdentityUser<Guid>, IAuditableEntity, ISoftDeletableEntity
    {
        public string FirstName { get; set; } = null!;
        public string LastName { get; set; } = null!;
        public string? Bio { get; set; }
        public bool IsPublic { get; set; }

        public bool IsBanned { get; set; }
        public DateTime? BannedUntil { get; set; }
        public string? BannedReason { get; set; }

        public bool IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public bool HasActiveBan() =>
            IsBanned && (!BannedUntil.HasValue || BannedUntil.Value > DateTime.UtcNow);

        public void Ban(TimeSpan? duration, string reason)
        {
            IsBanned = true;
            BannedUntil = duration.HasValue ? DateTime.UtcNow.Add(duration.Value) : null;
            BannedReason = reason;
        }

        public void Unban()
        {
            IsBanned = false;
            BannedUntil = null;
            BannedReason = null;
        }
    }
}
