using Api.Request.Trip;
using Domain.Const;
using FluentValidation;
using Domain.Enum;

namespace Api.Validators.Validators.Trip
{
    public sealed class UpsertTripRequestValidator : AbstractValidator<UpsertTripRequest>
    {
        public UpsertTripRequestValidator()
        {
            RuleFor(x => x.Title)
                .Cascade(CascadeMode.Stop)
                .Must(title => !string.IsNullOrWhiteSpace(title))
                .WithErrorCode(ErrorCodes.TripTitleInvalid)
                .MaximumLength(200)
                .WithErrorCode(ErrorCodes.TripTitleInvalid);

            RuleFor(x => x.Description)
                .MaximumLength(2000)
                .WithErrorCode(ErrorCodes.TripDescriptionInvalid);

            RuleFor(x => x.EndDate)
                .Must((request, endDate) =>
                    !endDate.HasValue || endDate.Value >= request.StartDate)
                .WithErrorCode(ErrorCodes.TripDateRangeInvalid);

            RuleFor(x => x.Segments)
                .Cascade(CascadeMode.Stop)
                .NotNull()
                .WithErrorCode(ErrorCodes.TripSegmentsInvalid)
                .NotEmpty()
                .WithErrorCode(ErrorCodes.TripSegmentsInvalid);


            RuleFor(x => x.Segments)
                .Must(HaveContinuousSegments)
                .WithErrorCode(ErrorCodes.TripSegmentsInvalid);

            RuleForEach(x => x.Segments)
                .SetValidator(new TripSegmentRequestValidator());
        }

        private static bool IsValidTransportType(string? transportType)
        {
            if (string.IsNullOrWhiteSpace(transportType))
            {
                return false;
            }

            foreach (var validTransportType in System.Enum.GetNames<TransportTypeEnum>())
            {
                if (string.Equals(
                        validTransportType,
                        transportType,
                        System.StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HaveContinuousSegments(
        IReadOnlyList<TripSegmentRequest>? segments)
        {
            if (segments is null || segments.Count < 2)
            {
                return true;
            }

            const double coordinateTolerance = 0.000001d;

            for (var index = 1; index < segments.Count; index++)
            {
                var previousDestination = segments[index - 1]?.Destination;
                var currentOrigin = segments[index]?.Origin;

                if (previousDestination is null || currentOrigin is null)
                {
                    continue;
                }

                if (Math.Abs(
                        previousDestination.Latitude - currentOrigin.Latitude) >
                        coordinateTolerance ||
                    Math.Abs(
                        previousDestination.Longitude - currentOrigin.Longitude) >
                        coordinateTolerance)
                {
                    return false;
                }
            }

            return true;
        }

        private sealed class TripSegmentRequestValidator
            : AbstractValidator<TripSegmentRequest>
        {
            public TripSegmentRequestValidator()
            {
                RuleFor(x => x.Origin)
                    .NotNull()
                    .WithErrorCode(ErrorCodes.TripPointInvalid)
                    .SetValidator(new TripPointRequestValidator());

                RuleFor(x => x.Destination)
                    .NotNull()
                    .WithErrorCode(ErrorCodes.TripPointInvalid)
                    .SetValidator(new TripPointRequestValidator());

                RuleFor(x => x.TransportType)
                    .Must(IsValidTransportType)
                    .WithErrorCode(ErrorCodes.TripTransportTypeInvalid);
            }
        }

        private sealed class TripPointRequestValidator
            : AbstractValidator<TripPointRequest>
        {
            public TripPointRequestValidator()
            {
                RuleFor(x => x.Name)
                    .Cascade(CascadeMode.Stop)
                    .Must(name => !string.IsNullOrWhiteSpace(name))
                    .WithErrorCode(ErrorCodes.TripPointInvalid)
                    .MaximumLength(255)
                    .WithErrorCode(ErrorCodes.TripPointInvalid);

                RuleFor(x => x.Latitude)
                    .Must(double.IsFinite)
                    .WithErrorCode(ErrorCodes.TripPointInvalid)
                    .InclusiveBetween(-90d, 90d)
                    .WithErrorCode(ErrorCodes.TripPointInvalid);

                RuleFor(x => x.Longitude)
                    .Must(double.IsFinite)
                    .WithErrorCode(ErrorCodes.TripPointInvalid)
                    .InclusiveBetween(-180d, 180d)
                    .WithErrorCode(ErrorCodes.TripPointInvalid);
            }
        }
    }
}