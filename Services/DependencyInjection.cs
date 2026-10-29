using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Services.Accessors;
using Services.Accessors.Interfaces;
using Services.Interfaces;
using Services.Interfaces.Auth;
using Services.Options;
using Services.Services;
using Services.Services.Auth;

namespace Services
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddScoped<ICancellationTokenAccessor, HttpCancellationTokenAccessor>();

            services.AddOptions<JwtOptions>()
                .Bind(configuration.GetSection(JwtOptions.SectionName))
                .Validate(JwtOptionsValidation.IsValid, JwtOptionsValidation.ErrorMessage)
                .ValidateOnStart();

            services.AddScoped<ITokenService, TokenService>();
            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<IUserServices, UserServices>();

            return services;
        }
    }
}
