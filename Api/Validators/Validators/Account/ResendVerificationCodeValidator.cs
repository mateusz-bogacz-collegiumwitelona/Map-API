using Api.Request.Account;
using Api.Validators.Rule;
using FluentValidation;

namespace Api.Validators.Validators.Account
{
    public class ResendVerificationCodeValidator : AbstractValidator<ResendVerificationCodeRequest>
    {
        public ResendVerificationCodeValidator()
        {
            RuleFor(x => x.Email).ApplyEmailRules();
        }
    }
}
