using Api.Request.Trip;
using Domain.Const;
using FluentValidation;

namespace Api.Validators.Validators.Trip
{
    public class TripFilterRequestValidator : AbstractValidator<TripFilterRequest>
    {
        public TripFilterRequestValidator()
        {
            RuleFor(x => x.UserId)
                .NotEqual(Guid.Empty)
                    .WithErrorCode(ErrorCodes.UserIdInvalid);

            RuleFor(x => x.Year)
                .Must(year => year >= TripConst.MinFilterYear && year <= MaxFilterYear())
                    .When(x => x.Year.HasValue)
                    .WithErrorCode(ErrorCodes.YearInvalid)
                    .WithMessage(x => $"Year must be between {TripConst.MinFilterYear} and {MaxFilterYear()}.");
        }

        private static int MaxFilterYear()
        {
            return DateTime.UtcNow.Year + TripConst.MaxFilterYearsAhead;
        }
    }
}
