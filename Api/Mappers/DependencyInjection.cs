namespace Api.Mappers
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddMappers(this IServiceCollection services)
        {
            services.AddSingleton<UserMapper>();
            return services;
        }
    }
}
