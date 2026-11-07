using Domain.Const;
using FluentValidation;

namespace Api.Validators.Rule
{
    public static class AuthValidationRules
    {
        public static IRuleBuilderOptions<T, string> ApplyEmailRules<T>(this IRuleBuilder<T, string> ruleBuilder)
            => ruleBuilder
                .NotEmpty().WithErrorCode(ErrorCodes.EmailInvalid)
                .MaximumLength(256).WithErrorCode(ErrorCodes.EmailInvalid)
                .EmailAddress().WithErrorCode(ErrorCodes.EmailInvalid);

        public static IRuleBuilderOptions<T, string> ApplyPasswordRules<T>(this IRuleBuilder<T, string> ruleBuilder)
            => ruleBuilder
                .NotEmpty().WithErrorCode(ErrorCodes.ValidationError);

        public static IRuleBuilderOptions<T, string> ApplyRefreshTokenRules<T>(this IRuleBuilder<T, string> ruleBuilder)
            => ruleBuilder
                .NotEmpty().WithErrorCode(ErrorCodes.ValidationError);

        public static IRuleBuilderOptions<T, string> ApplyCodeRules<T>(this IRuleBuilder<T, string> ruleBuilder)
            => ruleBuilder
                .NotEmpty()
                    .WithErrorCode(ErrorCodes.CodeInvalid)
                .Length(VerificationConst.CodeLength)
                    .WithErrorCode(ErrorCodes.CodeInvalid)
                .Must(x => x.All(char.IsAsciiDigit))
                    .WithErrorCode(ErrorCodes.CodeInvalid);
    }
}
