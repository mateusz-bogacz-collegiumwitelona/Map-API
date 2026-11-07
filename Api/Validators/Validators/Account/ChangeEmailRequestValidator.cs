using Api.Request.Account;
using Api.Validators.Rule;
using FluentValidation;

namespace Api.Validators.Validators.Account
{
    public class ChangeEmailRequestValidator : AbstractValidator<ChangeEmailRequest>
    {
        public ChangeEmailRequestValidator()
        {
            RuleFor(x => x.NewEmail).ApplyEmailRules();
        }
    }
}
