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
                RuleFor(x => x.FirstName!)
                    .Cascade(CascadeMode.Stop)
                    .Must(x => !string.IsNullOrWhiteSpace(x))
                    .WithErrorCode(ErrorCodes.UserFirstNameInvalid)
                    .MinimumLength(2)
                    .WithErrorCode(ErrorCodes.UserFirstNameInvalid)
                    .MaximumLength(50)
                    .WithErrorCode(ErrorCodes.UserFirstNameInvalid);
            });

            When(x => x.LastName != null, () =>
            {
                RuleFor(x => x.LastName!)
                    .Cascade(CascadeMode.Stop)
                    .Must(x => !string.IsNullOrWhiteSpace(x))
                    .WithErrorCode(ErrorCodes.UserLastNameInvalid)
                    .MinimumLength(2)
                    .WithErrorCode(ErrorCodes.UserLastNameInvalid)
                    .MaximumLength(50)
                    .WithErrorCode(ErrorCodes.UserLastNameInvalid);
            });

            When(x => x.Bio != null, () =>
            {
                RuleFor(x => x.Bio!)
                    .MaximumLength(500)
                    .WithErrorCode(ErrorCodes.UserBioInvalid);
            });

        }
    }
}
