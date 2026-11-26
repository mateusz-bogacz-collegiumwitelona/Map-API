namespace Api.Mappers
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddMappers(this IServiceCollection services)
        {
            services.AddSingleton<AuthMapper>();
            services.AddSingleton<UserMapper>();
            services.AddSingleton<BanMapper>();
            services.AddSingleton<ListMapper>();
            services.AddSingleton<TripMapper>();
            services.AddSingleton<PlaceMapper>();
            return services;
        }
    }
}
