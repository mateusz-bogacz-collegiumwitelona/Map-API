using Api.Request.List;
using Api.Validators.Rule;
using FluentValidation;

namespace Api.Validators.Validators.List
{
    public class BasicListValidator : AbstractValidator<ReportListRequest>
    {
        public BasicListValidator()
        {
            RuleFor(x => x.PageNumber).ApplyPageNumberRules();
            RuleFor(x => x.PageSize).ApplyPageSizeRules();
            RuleFor(x => x.SearchTerm).ApplySearchTermRules();
        }
    }
}
