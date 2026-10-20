using Domain.Enum;
using Domain.Models.Base;

namespace Domain.Models
{
    public class UserReport : BaseAuditableEntity
    {
        public Guid ReportedUserId { get; set; }
        public User ReportedUser { get; set; } = null!;

        public Guid ReporterUserId { get; set; }
        public User ReporterUser { get; set; } = null!;

        public string Reason { get; set; } = null!;
        public ReportStatusEnum Status { get; set; } = ReportStatusEnum.Pending;

        public Guid? ResolvedByAdminId { get; set; }
        public User? ResolvedByAdmin { get; set; }
        public DateTime? ResolvedAt { get; set; }
    }
}
