namespace Api.Configuration
{
    public static class RoutingConfiguration
    {
        public static IServiceCollection AddRoutingConfiguration(
            this IServiceCollection services)
        {
            services.AddRouting(options =>
            {
                options.LowercaseUrls = true;
            });
            return services;
        }
    }
}
