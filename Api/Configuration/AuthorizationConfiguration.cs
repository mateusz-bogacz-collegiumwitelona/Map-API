namespace Api.Configuration
{
    public static class AuthorizationConfiguration
    {
        public const string UserPolicy = "User";
        public const string AdminPolicy = "Admin";

        public static IServiceCollection AddAuthorizationConfiguration(this IServiceCollection services)
        {
            services.AddAuthorization(options =>
            {
                options.AddPolicy(UserPolicy, policy => policy.RequireRole("User"));
                options.AddPolicy(AdminPolicy, policy => policy.RequireRole("Admin"));
            });

            return services;
        }
    }
}
