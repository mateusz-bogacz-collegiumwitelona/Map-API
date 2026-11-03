using Api.Mappers.Helper;
using Api.Request.Auth;
using Riok.Mapperly.Abstractions;
using Services.Command.Auth;
using Services.Commands.Auth;

namespace Api.Mappers
{
    [Mapper]
    public partial class AuthMapper : BaseMapper
    {
        public partial LoginCommand ToLoginCommand(LoginRequest request);
        public RegisterCommand ToRegisterCommand(RegisterRequest request)
            => new RegisterCommand
            {
                FirstName = NormalizeRequiredName(request.FirstName),
                LastName = NormalizeRequiredName(request.LastName),
                Email = request.Email.Trim(),
                Password = request.Password,
                Bio = request.Bio
            };
    }
}
