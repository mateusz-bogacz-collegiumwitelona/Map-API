using Api.Request.Trip;
using Api.Validators.Rule;
using Domain.Const;
using FluentValidation;

namespace Api.Validators.Validators.Trip
{
    public class UpdateSegmentRouteRequestValidator : AbstractValidator<UpdateSegmentRouteRequest>
    {
        public UpdateSegmentRouteRequestValidator()
        {
            RuleFor(x => x.Coordinates)
                .NotNull()
                .WithErrorCode(ErrorCodes.RouteInvalid);

            RuleFor(x => x.Coordinates.Count)
                .ApplyRoutePointCountRules()
                .When(x => x.Coordinates != null)
                .DependentRules(() =>
                {
                    RuleForEach(x => x.Coordinates)
                        .ApplyRoutePointRules();
                });

            RuleFor(x => x.DistanceKm)
                .ApplyRouteDistanceRules();
        }
    }
}
