using Api.Request.User;
using Api.Validators.Rule;
using FluentValidation;

namespace Api.Validators.Validators.User
{
    public class UserSearchRequestValidator : AbstractValidator<UserSearchRequest>
    {
        public UserSearchRequestValidator()
        {
            RuleFor(x => x.Q).ApplySearchTermRules();
            RuleFor(x => x.Sort).ApplyUserSortRules();
            RuleFor(x => x.Page).ApplyPageNumberRules();
            RuleFor(x => x.Limit).ApplyPageSizeRules();
        }
    }
}
