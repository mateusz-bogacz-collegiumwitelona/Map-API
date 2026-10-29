using Api.Request.Auth;
using Api.Validators.Rule;
using FluentValidation;

namespace Api.Validators.Validators.Auth
{
    public class LogoutRequestValidator : AbstractValidator<LogoutRequest>
    {
        public LogoutRequestValidator()
        {
            RuleFor(x => x.RefreshToken).ApplyRefreshTokenRules();
        }
    }
}
