using Api.Mappers.Helper;
using Api.Request.Auth;
using Api.Request.User;
using Riok.Mapperly.Abstractions;
using Services.Command.Auth;
using Services.Command.User;
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

        [MapProperty(nameof(ConfirmEmailRequest.Code), nameof(ConfirmEmailCommand.Code), Use = nameof(Trim))]
        [MapProperty(nameof(ConfirmEmailRequest.Email), nameof(ConfirmEmailCommand.Email), Use = nameof(Trim))]
        public partial ConfirmEmailCommand ToConfirmEmail(ConfirmEmailRequest request);
    }
}
