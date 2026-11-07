using Api.Request.User;
using Domain.Const;
using FluentValidation;

namespace Api.Validators.User
{
    public class ConfirmEmailChangeRequestValidator : AbstractValidator<ConfirmEmailChangeRequest>
    {
        public ConfirmEmailChangeRequestValidator()
        {
            RuleFor(x => x.Code)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                    .WithErrorCode(ErrorCodes.CodeInvalid)
                .Length(VerificationConst.CodeLength)
                    .WithErrorCode(ErrorCodes.CodeInvalid)
                .Must(x => x.All(char.IsAsciiDigit))
                    .WithErrorCode(ErrorCodes.CodeInvalid);
        }
    }
}
