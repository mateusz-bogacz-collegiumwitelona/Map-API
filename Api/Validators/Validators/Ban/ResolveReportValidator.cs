using Api.Request.Ban;
using Api.Validators.Rule;
using Domain.Const;
using FluentValidation;

namespace Api.Validators.Validators.Ban
{
    public class ResolveReportValidator : AbstractValidator<ResolveReportRequest>
    {
        public ResolveReportValidator()
        {
            RuleFor(x => x.Id).ApplyGuidRules();

            When(x => x.Duration != null, () =>
            {
                RuleFor(x => x.Duration)
                    .GreaterThan(TimeSpan.Zero)
                    .WithErrorCode(ErrorCodes.BanDurationInvalid)
                    .LessThanOrEqualTo(TimeSpan.FromDays(365))
                    .WithErrorCode(ErrorCodes.BanDurationInvalid);
            });

            When(x => !x.IsBanned, () =>
            {
                RuleFor(x => x.Duration)
                    .Null()
                    .WithErrorCode(ErrorCodes.BanDurationNotAllowed);
            });
        }
    }
}
