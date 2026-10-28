using Api.Request.Auth;
using Riok.Mapperly.Abstractions;
using Services.Commands.Auth;

namespace Api.Mappers
{
    [Mapper]
    public partial class AuthMapper
    {
        public partial LoginCommand ToLoginCommand(LoginRequest request);
    }
}
