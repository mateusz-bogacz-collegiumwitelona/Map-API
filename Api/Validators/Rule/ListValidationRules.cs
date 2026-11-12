using Domain.Const;
using FluentValidation;

namespace Api.Validators.Rule
{
    public static class ListValidationRules
    {
        public static IRuleBuilderOptions<T, int?> ApplyPageNumberRules<T>(
           this IRuleBuilder<T, int?> ruleBuilder,
           string errorCode = ErrorCodes.ValidationError)
               => ruleBuilder
               .GreaterThan(0)
               .WithErrorCode(ErrorCodes.PageNumberInvalid);

        public static IRuleBuilderOptions<T, int?> ApplyPageSizeRules<T>(
            this IRuleBuilder<T, int?> ruleBuilder,
            string errorCode = ErrorCodes.ValidationError)
                => ruleBuilder
                    .GreaterThan(0)
                    .WithErrorCode(ErrorCodes.PageSizeInvalid)
                    .LessThanOrEqualTo(100)
                    .WithErrorCode(ErrorCodes.PageSizeInvalid);
    }
}
