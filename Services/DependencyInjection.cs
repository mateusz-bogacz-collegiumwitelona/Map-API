using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Services.Accessors;
using Services.Accessors.Interfaces;

namespace Services
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
        {
            // Add application services here
            // Example: services.AddScoped<IMyService, MyService>();
            services.AddScoped<ICancellationTokenAccessor, HttpCancellationTokenAccessor>();

            return services;
        }
    }
}
