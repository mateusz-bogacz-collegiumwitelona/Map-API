using Domain.Const;
using FluentValidation;

namespace Api.Validators.Rule
{
    public static class ListValidationRules
    {
        public static IRuleBuilderOptions<T, int?> ApplyPageNumberRules<T>(
           this IRuleBuilder<T, int?> ruleBuilder)
               => ruleBuilder
               .GreaterThan(0)
               .WithErrorCode(ErrorCodes.PageNumberInvalid);

        public static IRuleBuilderOptions<T, int?> ApplyPageSizeRules<T>(
            this IRuleBuilder<T, int?> ruleBuilder,
            string errorCode = ErrorCodes.PageSizeInvalid)
                => ruleBuilder
                    .GreaterThan(0)
                    .WithErrorCode(errorCode)
                    .LessThanOrEqualTo(100)
                    .WithErrorCode(errorCode);

        public static IRuleBuilderOptions<T, string?> ApplySearchTermRules<T>(
            this IRuleBuilder<T, string?> ruleBuilder,
            string errorCode = ErrorCodes.ValidationError)
                => ruleBuilder
                    .MaximumLength(100)
                    .WithErrorCode(errorCode);
    }
}
