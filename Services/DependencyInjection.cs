using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Services.Accessors;
using Services.Accessors.Interfaces;
using Services.Interfaces;
using Services.Services;

namespace Services
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddScoped<ICancellationTokenAccessor, HttpCancellationTokenAccessor>();
            services.AddScoped<IUserServices, UserServices>();
            return services;
        }
    }
}
