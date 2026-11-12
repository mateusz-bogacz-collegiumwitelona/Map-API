using Api.Request.Account;
using Api.Validators.Rule;
using FluentValidation;

namespace Api.Validators.Validators.Account
{
    public class ConfirmEmailChangeRequestValidator : AbstractValidator<ConfirmEmailChangeRequest>
    {
        public ConfirmEmailChangeRequestValidator()
        {
            RuleFor(x => x.Code).ApplyCodeRules();
        }
    }
}
