using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Services.Accessors;
using Services.Accessors.Interfaces;
using Services.Interfaces.Auth;
using Services.Options;
using Services.Services.Auth;

namespace Services
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
        {
            // Add application services here
            // Example: services.AddScoped<IMyService, MyService>();
            services.AddScoped<ICancellationTokenAccessor, HttpCancellationTokenAccessor>();

            services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
            services.AddScoped<ITokenService, TokenService>();
            services.AddScoped<IAuthService, AuthService>();

            return services;
        }
    }
}
