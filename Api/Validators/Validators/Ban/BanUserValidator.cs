using Api.Request.Ban;
using Api.Validators.Rule;
using FluentValidation;

namespace Api.Validators.Validators.Ban
{
    public class BanUserValidator : AbstractValidator<BanUserRequest>
    {
        public BanUserValidator()
        {
            RuleFor(x => x.UserId).ApplyGuidRules();
            RuleFor(x => x.Reason).ApplyBanReasonRules();

            When(x => x.Duration != null, () =>
            {
                RuleFor(x => x.Duration).ApplyBanDurationRules();
            });
        }
    }
}
