using Domain.Const;
using FluentValidation;

namespace Api.Validators.Rule
{
    public static class CommonValidationRules
    {
        public static IRuleBuilderOptions<T, Guid?> ApplyGuidRules<T>(this IRuleBuilder<T, Guid?> ruleBuilder)
            => ruleBuilder
                .NotEqual(Guid.Empty)
                .WithErrorCode(ErrorCodes.GuidInvalid);
    }
}
