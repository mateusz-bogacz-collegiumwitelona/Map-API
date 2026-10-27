using Api.Request.Auth;
using Api.Validators.Rule;
using FluentValidation;

namespace Api.Validators.Validators.Auth
{
    public class RefreshTokenRequestValidator : AbstractValidator<RefreshTokenRequest>
    {
        public RefreshTokenRequestValidator()
        {
            RuleFor(x => x.RefreshToken).ApplyRefreshTokenRules();
        }
    }
}
