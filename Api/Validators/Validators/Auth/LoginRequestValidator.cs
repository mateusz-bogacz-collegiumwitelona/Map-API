using Api.Request.Auth;
using Api.Validators.Rule;
using FluentValidation;

namespace Api.Validators.Validators.Auth
{
    public class LoginRequestValidator : AbstractValidator<LoginRequest>
    {
        public LoginRequestValidator()
        {
            RuleFor(x => x.Email).ApplyEmailRules();
            RuleFor(x => x.Password).ApplyPasswordRules();
        }
    }
}
