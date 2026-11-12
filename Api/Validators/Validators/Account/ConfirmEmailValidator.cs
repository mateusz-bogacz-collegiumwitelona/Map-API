using Api.Request.Account;
using Api.Validators.Rule;
using FluentValidation;

namespace Api.Validators.Validators.Account
{
    public class ConfirmEmailValidator : AbstractValidator<ConfirmEmailRequest>
    {
        public ConfirmEmailValidator()
        {
            RuleFor(x => x.Email).ApplyEmailRules();
            RuleFor(x => x.Code).ApplyCodeRules();
        }
    }
}
