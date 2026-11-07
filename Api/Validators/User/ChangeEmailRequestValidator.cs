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
                    .WithErrorCode(ErrorCodes.EmailInvalid)
                .MaximumLength(256)
                    .WithErrorCode(ErrorCodes.EmailInvalid)
                .EmailAddress()
                    .WithErrorCode(ErrorCodes.EmailInvalid);
        }
    }
}
