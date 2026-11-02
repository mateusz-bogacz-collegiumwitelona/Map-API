using Api.Request.User;
using Domain.Const;
using FluentValidation;
using System.Net.Mail;

namespace Api.Validators.User
{
    public class ChangeEmailRequestValidator : AbstractValidator<ChangeEmailRequest>
    {
        public ChangeEmailRequestValidator()
        {
            RuleFor(x => x.NewEmail)
                .Cascade(CascadeMode.Stop)
                .Must(x => !string.IsNullOrWhiteSpace(x))
                .WithErrorCode(ErrorCodes.User.EmailInvalid)
                .Must(x => x.Trim().Length <= 256)
                .WithErrorCode(ErrorCodes.User.EmailInvalid)
                .Must(x => IsValidEmail(x.Trim()))
                .WithErrorCode(ErrorCodes.User.EmailInvalid);
        }

        private static bool IsValidEmail(string email)
            => MailAddress.TryCreate(email, out var address) && address.Address == email;
    }
}
