using Api.Request.Trip;
using Domain.Const;
using FluentValidation;

namespace Api.Validators.Rule
{
    public static class TripValidationRules
    {
        public static IRuleBuilderOptions<T, int> ApplyRoutePointCountRules<T>(this IRuleBuilder<T, int> ruleBuilder)
            => ruleBuilder
                .InclusiveBetween(TripConst.MinRoutePoints, TripConst.MaxRoutePoints)
                .WithErrorCode(ErrorCodes.RouteInvalid);

        public static IRuleBuilderOptions<T, RoutePointRequest> ApplyRoutePointRules<T>(this IRuleBuilder<T, RoutePointRequest> ruleBuilder)
            => ruleBuilder
                .NotNull()
                .WithErrorCode(ErrorCodes.RouteInvalid)
                .ChildRules(point =>
                {
                    point.RuleFor(p => p.Latitude)
                        .InclusiveBetween(GeoConst.MinLatitude, GeoConst.MaxLatitude)
                        .WithErrorCode(ErrorCodes.RouteInvalid);

                    point.RuleFor(p => p.Longitude)
                        .InclusiveBetween(GeoConst.MinLongitude, GeoConst.MaxLongitude)
                        .WithErrorCode(ErrorCodes.RouteInvalid);
                });

        public static IRuleBuilderOptions<T, decimal> ApplyRouteDistanceRules<T>(this IRuleBuilder<T, decimal> ruleBuilder)
            => ruleBuilder
                .InclusiveBetween(TripConst.MinRouteDistanceKm, TripConst.MaxRouteDistanceKm)
                .WithErrorCode(ErrorCodes.DistanceInvalid)
                .WithMessage(FormattableString.Invariant($"Distance must be between {TripConst.MinRouteDistanceKm} and {TripConst.MaxRouteDistanceKm} km."));
    }
}
