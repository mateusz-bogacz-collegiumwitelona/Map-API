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

            When(x => x.Coordinates != null, () =>
            {
                RuleFor(x => x.Coordinates.Count)
                    .ApplyRoutePointCountRules()
                    .DependentRules(() =>
                    {
                        RuleForEach(x => x.Coordinates)
                            .ApplyRoutePointRules();
                    });
            });

            RuleFor(x => x.DistanceKm)
                .ApplyRouteDistanceRules();
        }
    }
}
