using Api.Request.Ban;
using Api.Validators.Rule;
using FluentValidation;

namespace Api.Validators.Validators.Ban
{
    public class ReportUserValidator : AbstractValidator<ReportUserRequest>
    {
        public ReportUserValidator()
        {
            RuleFor(x => x.UserId).ApplyGuidRules();
            RuleFor(x => x.Reason).ApplyBanReasonRules();
        }
    }
}
