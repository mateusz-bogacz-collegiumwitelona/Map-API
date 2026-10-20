using Domain.Models.Base;

namespace Domain.Models
{
    public class EmailVerificationCode : BaseAuditableEntity
    {
        public Guid UserId { get; set; }
        public User User { get; set; } = null!;

        public string Code { get; set; } = null!;
        public DateTime ExpiredAt { get; set; }
        public int AttemptsCount { get; set; }
        public bool IsUsed { get; set; }
    }
}
