using Api.Request.User;
using Domain.Const;
using FluentValidation;

namespace Api.Validators.User
{
    public class ChangeEmailRequestValidator : AbstractValidator<ChangeEmailRequest>
    {
        public ChangeEmailRequestValidator()
        {
            RuleFor(x => x.NewEmail)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                    .WithErrorCode(ErrorCodes.User.EmailInvalid)
                .MaximumLength(256)
                    .WithErrorCode(ErrorCodes.User.EmailInvalid)
                .EmailAddress()
                    .WithErrorCode(ErrorCodes.User.EmailInvalid);
        }
    }
}
