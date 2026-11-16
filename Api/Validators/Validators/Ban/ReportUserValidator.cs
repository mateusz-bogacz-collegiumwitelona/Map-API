using Api.Request.Ban;
using Api.Validators.Rule;
using Domain.Const;
using FluentValidation;

namespace Api.Validators.Validators.Ban
{
    public class ReportUserValidator : AbstractValidator<ReportUserRequest>
    {
        public ReportUserValidator()
        {
            RuleFor(x => x.UserId).ApplyGuidRules();
            RuleFor(x => x.Reason)
                .NotEmpty()
                .WithErrorCode(ErrorCodes.ReportEmpty)
                .MinimumLength(5)
                .WithErrorCode(ErrorCodes.ReportTooShort)
                .MaximumLength(500)
                .WithErrorCode(ErrorCodes.ReportTooLong);
        }
    }
}
