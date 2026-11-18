using Domain.Enum;
using Domain.Enum.Trigger;
using Domain.Models;
using Stateless;

namespace Domain.State
{
    public class UserReportStateMachine
    {
        private readonly UserReport _report;
        private readonly StateMachine<ReportStatusEnum, ReportTriggerEnum> _machine;
        private readonly StateMachine<ReportStatusEnum, ReportTriggerEnum>.TriggerWithParameters<Guid> _approveTrigger;
        private readonly StateMachine<ReportStatusEnum, ReportTriggerEnum>.TriggerWithParameters<Guid> _rejectTrigger;

        public UserReportStateMachine(UserReport report)
        {
            _report = report ?? throw new ArgumentNullException(nameof(report));

            _machine = new StateMachine<ReportStatusEnum, ReportTriggerEnum>(
                () => _report.Status,
                status => _report.Status = status
            );

            _approveTrigger = _machine.SetTriggerParameters<Guid>(ReportTriggerEnum.Approve);
            _rejectTrigger = _machine.SetTriggerParameters<Guid>(ReportTriggerEnum.Reject);

            Configure();
        }

        private void Configure()
        {

            _machine.Configure(ReportStatusEnum.Pending)
                .Permit(ReportTriggerEnum.Approve, ReportStatusEnum.Approved)
                .Permit(ReportTriggerEnum.Reject, ReportStatusEnum.Rejected);

            _machine.Configure(ReportStatusEnum.Approved)
                .OnEntryFrom(_approveTrigger, adminId => ResolveReport(adminId));

            _machine.Configure(ReportStatusEnum.Rejected)
                .OnEntryFrom(_rejectTrigger, adminId => ResolveReport(adminId));
        }

        private void ResolveReport(Guid adminId)
        {
            _report.ResolvedByAdminId = adminId;
            _report.ResolvedAt = DateTime.UtcNow;
        }

        public void Approve(Guid adminId)
            => _machine.Fire(_approveTrigger, adminId);

        public void Reject(Guid adminId)
            => _machine.Fire(_rejectTrigger, adminId);

        public bool CanApprove()
            => _machine.CanFire(ReportTriggerEnum.Approve);

        public bool CanReject()
            => _machine.CanFire(ReportTriggerEnum.Reject);
    }
}
