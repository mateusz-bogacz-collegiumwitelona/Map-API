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
                .Must(x => !string.IsNullOrWhiteSpace(x))
                .WithErrorCode(ErrorCodes.Verification.CodeInvalid)
                .Must(x => x.Trim().Length == VerificationConst.CodeLength)
                .WithErrorCode(ErrorCodes.Verification.CodeInvalid)
                .Must(x => x.Trim().All(char.IsAsciiDigit))
                .WithErrorCode(ErrorCodes.Verification.CodeInvalid);
        }
    }
}
