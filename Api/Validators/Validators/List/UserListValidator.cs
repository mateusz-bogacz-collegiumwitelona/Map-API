using Api.Request.List;
using Api.Validators.Rule;
using FluentValidation;

namespace Api.Validators.Validators.List
{
    public class UserListValidator : AbstractValidator<UserListRequest>
    {
        public UserListValidator()
        {
            RuleFor(x => x.PageNumber).ApplyPageNumberRules();
            RuleFor(x => x.PageSize).ApplyPageSizeRules();
            RuleFor(x => x.SearchTerm).ApplySearchTermRules();
        }
    }
}
