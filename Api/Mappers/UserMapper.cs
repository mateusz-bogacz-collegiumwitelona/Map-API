using Api.Mappers.Helper;
using Api.Request.User;
using Riok.Mapperly.Abstractions;
using Services.Command.User;

namespace Api.Mappers
{
    [Mapper]
    public partial class UserMapper : BaseMapper
    {
        [MapProperty(nameof(EditUserRequest.FirstName), nameof(EditUserCommand.FirstName), Use = nameof(NormalizeNullableName))]
        [MapProperty(nameof(EditUserRequest.LastName), nameof(EditUserCommand.LastName), Use = nameof(NormalizeNullableName))]
        public partial EditUserCommand MapEdit(EditUserRequest request);
    }
}
