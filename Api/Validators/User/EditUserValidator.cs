using Api.Request.User;
using Domain.Const;
using FluentValidation;

namespace Api.Validators.User
{
    public class EditUserValidator : AbstractValidator<EditUserRequest>
    {
        public EditUserValidator()
        {
            When(x => x.FirstName != null, () =>
            {
                RuleFor(x => x.FirstName)
                    .Cascade(CascadeMode.Stop)
                    .Must(x => !string.IsNullOrWhiteSpace(x))
                    .WithErrorCode(ErrorCodes.User.FirstNameInvalid)
                    .Must(x => x!.Trim().Length >= 2)
                    .WithErrorCode(ErrorCodes.User.FirstNameInvalid)
                    .Must(x => x!.Trim().Length <= 50)
                    .WithErrorCode(ErrorCodes.User.FirstNameInvalid);
            });

            When(x => x.LastName != null, () =>
            {
                RuleFor(x => x.LastName)
                    .Cascade(CascadeMode.Stop)
                    .Must(x => !string.IsNullOrWhiteSpace(x))
                    .WithErrorCode(ErrorCodes.User.LastNameInvalid)
                    .Must(x => x!.Trim().Length >= 2)
                    .WithErrorCode(ErrorCodes.User.LastNameInvalid)
                    .Must(x => x!.Trim().Length <= 50)
                    .WithErrorCode(ErrorCodes.User.LastNameInvalid);
            });

            When(x => x.Bio != null, () =>
            {
                RuleFor(x => x.Bio)
                    .Must(x => x!.Trim().Length <= 500)
                    .WithErrorCode(ErrorCodes.User.BioInvalid);
            });
        }
    }
}