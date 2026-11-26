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
                .NotEmpty()
                .WithErrorCode(ErrorCodes.PlaceSearchQueryInvalid)
                .Length(2, 100)
                .WithErrorCode(ErrorCodes.PlaceSearchQueryInvalid);
        }
    }
}