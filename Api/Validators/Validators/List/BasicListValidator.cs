using Api.Request.List;
using Api.Validators.Rule;
using FluentValidation;

namespace Api.Validators.Validators.List
{
    public class BasicListValidator : AbstractValidator<BasicListRequest>
    {
        public BasicListValidator()
        {
            RuleFor(x => x.PageNumber).ApplyPageNumberRules();
            RuleFor(x => x.PageSize).ApplyPageSizeRules();
        }
    }
}
