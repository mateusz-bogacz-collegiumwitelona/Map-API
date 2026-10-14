using Infrastructure;

namespace Api.Configuration
{
    public static class HealthCheckConfiguration
    {
        public static IServiceCollection AddHealthCheckConfiguration(this IServiceCollection services)
        {
            services.AddHealthChecks()
                .AddDbContextCheck<AppDbContext>("database");
            return services;
        }
    }
}
