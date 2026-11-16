using Domain.Const;
using FluentValidation;

namespace Api.Validators.Rule
{
    public static class BanValidationRules
    {
        public static IRuleBuilderOptions<T, string> ApplyBanReasonRules<T>(this IRuleBuilder<T, string> ruleBuilder)
            => ruleBuilder
                .NotEmpty()
                    .WithErrorCode(ErrorCodes.ReportEmpty)
                .MinimumLength(5)
                    .WithErrorCode(ErrorCodes.ReportTooShort)
                .MaximumLength(500)
                    .WithErrorCode(ErrorCodes.ReportTooLong);

        public static IRuleBuilderOptions<T, TimeSpan?> ApplyBanDurationRules<T>(this IRuleBuilder<T, TimeSpan?> ruleBuilder)
            => ruleBuilder
                .GreaterThan(TimeSpan.Zero)
                    .WithErrorCode(ErrorCodes.BanDurationInvalid)
                .LessThanOrEqualTo(TimeSpan.FromDays(365))
                    .WithErrorCode(ErrorCodes.BanDurationInvalid);
    }
}
