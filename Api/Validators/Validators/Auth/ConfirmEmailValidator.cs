using Api.Request.Auth;
using Api.Validators.Rule;
using Domain.Const;
using FluentValidation;

namespace Api.Validators.Validators.Auth
{
    public class ConfirmEmailValidator : AbstractValidator<ConfirmEmailRequest>
    {
        public ConfirmEmailValidator()
        {
            RuleFor(x => x.Email).ApplyEmailRules();
            RuleFor(x => x.Code)
                .Cascade(CascadeMode.Stop)
                .NotEmpty().WithErrorCode(ErrorCodes.InvalidVerificationCode)
                .Length(6).WithErrorCode(ErrorCodes.InvalidVerificationCode)
                .Matches(@"^[0-9]{6}$").WithErrorCode(ErrorCodes.InvalidVerificationCode);
        }
    }
}
