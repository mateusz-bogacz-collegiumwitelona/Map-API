using Api.Request.Auth;
using Api.Validators.Rule;
using Domain.Const;
using FluentValidation;

namespace Api.Validators.Validators.Auth
{
    public class RegisterValidator : AbstractValidator<RegisterRequest>
    {
        public RegisterValidator()
        {
            RuleFor(x => x.FirstName).ApplyFirstNameRules();

            RuleFor(x => x.LastName).ApplyLastNameRules();

            RuleFor(x => x.Email).ApplyEmailRules();

            RuleFor(x => x.Password).ApplyPasswordRules();

            RuleFor(x => x.ConfirmPassword)
                .NotEmpty().WithErrorCode(ErrorCodes.ValidationError)
                .Equal(x => x.Password).WithErrorCode(ErrorCodes.ValidationError);

            When(x => x.Bio != null, () =>
            {
                RuleFor(x => x.Bio).ApplyBioRules();
            });
        }
    }
}
