namespace Api.Mappers
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddMappers(this IServiceCollection services)
        {
            services.AddSingleton<AuthMapper>();
            services.AddSingleton<UserMapper>();
            services.AddSingleton<ListMapper>();
            return services;
        }
    }
}
