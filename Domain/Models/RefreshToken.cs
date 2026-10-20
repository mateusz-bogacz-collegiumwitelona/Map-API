using Domain.Models.Base;

namespace Domain.Models
{
    public class RefreshToken : BaseAuditableEntity
    {
        public string Token { get; set; } = null!;

        public Guid UserId { get; set; }
        public User User { get; set; } = null!;

        public DateTime ExpiredAt { get; set; }
        public bool IsRevoked { get; set; }
    }
}
