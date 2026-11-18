using Api.Request.Place;
using Domain.Const;
using FluentValidation;

namespace Api.Validators.Validators.Place
{
    public sealed class PlaceSearchRequestValidator
        : AbstractValidator<PlaceSearchRequest>
    {
        public PlaceSearchRequestValidator()
        {
            RuleFor(x => x.Query)
                .Cascade(CascadeMode.Stop)
                .Must(query => !string.IsNullOrWhiteSpace(query))
                .WithErrorCode(ErrorCodes.PlaceSearchQueryInvalid)
                .Must(query => query!.Trim().Length >= 2)
                .WithErrorCode(ErrorCodes.PlaceSearchQueryInvalid)
                .Must(query => query!.Trim().Length <= 100)
                .WithErrorCode(ErrorCodes.PlaceSearchQueryInvalid);
        }
    }
}