using Domain.Const;
using FluentValidation;

namespace Api.Validators.Rule
{
    public static class AuthValidationRules
    {
        public static IRuleBuilderOptions<T, string> ApplyEmailRules<T>(this IRuleBuilder<T, string> ruleBuilder)
            => ruleBuilder
                .NotEmpty().WithErrorCode(ErrorCodes.ValidationError)
                .EmailAddress().WithErrorCode(ErrorCodes.ValidationError);

        public static IRuleBuilderOptions<T, string> ApplyPasswordRules<T>(this IRuleBuilder<T, string> ruleBuilder)
            => ruleBuilder
                .NotEmpty().WithErrorCode(ErrorCodes.ValidationError);

        public static IRuleBuilderOptions<T, string> ApplyRefreshTokenRules<T>(this IRuleBuilder<T, string> ruleBuilder)
            => ruleBuilder
                .NotEmpty().WithErrorCode(ErrorCodes.ValidationError);
    }
}
