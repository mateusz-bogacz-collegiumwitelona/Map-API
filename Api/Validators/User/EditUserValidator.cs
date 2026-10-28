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
                    .WithErrorCode(ErrorCodes.UserFirstNameInvalid)
                    .Must(x => x!.Trim().Length >= 2)
                    .WithErrorCode(ErrorCodes.UserFirstNameInvalid)
                    .Must(x => x!.Trim().Length <= 50)
                    .WithErrorCode(ErrorCodes.UserFirstNameInvalid);
            });

            When(x => x.LastName != null, () =>
            {
                RuleFor(x => x.LastName)
                    .Cascade(CascadeMode.Stop)
                    .Must(x => !string.IsNullOrWhiteSpace(x))
                    .WithErrorCode(ErrorCodes.UserLastNameInvalid)
                    .Must(x => x!.Trim().Length >= 2)
                    .WithErrorCode(ErrorCodes.UserLastNameInvalid)
                    .Must(x => x!.Trim().Length <= 50)
                    .WithErrorCode(ErrorCodes.UserLastNameInvalid);
            });

            When(x => x.Bio != null, () =>
            {
                RuleFor(x => x.Bio)
                    .Must(x => x!.Trim().Length <= 500)
                    .WithErrorCode(ErrorCodes.UserBioInvalid);
            });
        }
    }
}