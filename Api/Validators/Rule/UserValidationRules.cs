using Domain.Const;
using FluentValidation;

namespace Api.Validators.Rule
{
    public static class UserValidationRules
    {
        public static IRuleBuilderOptions<T, string?> ApplyFirstNameRules<T>(this IRuleBuilderInitial<T, string?> ruleBuilder)
            => ruleBuilder.ApplyPersonaNamesRules(ErrorCodes.FirstNameInvalid);

        public static IRuleBuilderOptions<T, string?> ApplyLastNameRules<T>(this IRuleBuilderInitial<T, string?> ruleBuilder)
            => ruleBuilder.ApplyPersonaNamesRules(ErrorCodes.LastNameInvalid);

        public static IRuleBuilderOptions<T, string?> ApplyBioRules<T>(this IRuleBuilder<T, string?> ruleBuilder)
            => ruleBuilder
                .Must((string? val) => val == null || val.Trim().Length <= 500)
                .WithErrorCode(ErrorCodes.BioInvalid);

        public static IRuleBuilderOptions<T, string> ApplyStrongPasswordRules<T>(this IRuleBuilder<T, string> ruleBuilder)
            => ruleBuilder
                .NotEmpty().WithErrorCode(ErrorCodes.ValidationError)
                .MinimumLength(8).WithErrorCode(ErrorCodes.ValidationError)
                .Matches(@"[A-Z]").WithErrorCode(ErrorCodes.ValidationError)
                .Matches(@"[0-9]").WithErrorCode(ErrorCodes.ValidationError)
                .Matches(@"[\W_]").WithErrorCode(ErrorCodes.ValidationError);

        private static IRuleBuilderOptions<T, string?> ApplyPersonaNamesRules<T>(this IRuleBuilderInitial<T, string?> ruleBuilder, string errorCode)
           => ruleBuilder
               .Cascade(CascadeMode.Stop)
               .Must((string? val) => !string.IsNullOrWhiteSpace(val))
               .WithErrorCode(errorCode)
               .Must((string? val) => val!.Trim().Length >= 2)
               .WithErrorCode(errorCode)
               .Must((string? val) => val!.Trim().Length <= 50)
               .WithErrorCode(errorCode);
    }
}