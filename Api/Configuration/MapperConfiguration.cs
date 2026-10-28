using Api.Mappers;

namespace Api.Configuration
{
    public static class MapperConfiguration
    {
        public static IServiceCollection AddMapperConfiguration(
            this IServiceCollection services)
        {
            services.AddSingleton<AuthMapper>();
            return services;
        }
    }
}
