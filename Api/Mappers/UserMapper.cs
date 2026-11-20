using Api.Mappers.Helper;
using Api.Request.Account;
using Api.Request.User;
using Riok.Mapperly.Abstractions;
using Services.Command.Account;
using Services.Command.User;

namespace Api.Mappers
{
    [Mapper]
    public partial class UserMapper : BaseMapper
    {
        [MapProperty(nameof(EditUserRequest.FirstName), nameof(EditUserCommand.FirstName), Use = nameof(NormalizeNullableName))]
        [MapProperty(nameof(EditUserRequest.LastName), nameof(EditUserCommand.LastName), Use = nameof(NormalizeNullableName))]
        public partial EditUserCommand MapEdit(EditUserRequest request);

        [MapProperty(nameof(ChangeEmailRequest.NewEmail), nameof(ChangeEmailCommand.NewEmail), Use = nameof(Trim))]
        public partial ChangeEmailCommand MapChangeEmail(ChangeEmailRequest request);

        [MapProperty(nameof(UserSearchRequest.Q), nameof(UserSearchCommand.Query))]
        [MapProperty(nameof(UserSearchRequest.Page), nameof(UserSearchCommand.PageNumber))]
        [MapProperty(nameof(UserSearchRequest.Limit), nameof(UserSearchCommand.PageSize))]
        public partial UserSearchCommand MapUserSearch(UserSearchRequest request);

        public partial ConfirmEmailChangeCommand MapConfirmEmailChange(ConfirmEmailChangeRequest request);
    }
}
