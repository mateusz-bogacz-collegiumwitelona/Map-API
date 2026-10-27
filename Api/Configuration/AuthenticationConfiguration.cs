using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Services.Options;
using System.Text;

namespace Api.Configuration
{
    public static class AuthenticationConfiguration
    {
        public static IServiceCollection AddAuthenticationConfiguration(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            var jwtOptions = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
                ?? throw new InvalidOperationException("JWT configuration section is missing.");

            Validate(jwtOptions);

            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = jwtOptions.Issuer,
                        ValidateAudience = true,
                        ValidAudience = jwtOptions.Audience,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SecretKey)),
                        ClockSkew = TimeSpan.Zero
                    };
                });

            return services;
        }

        private static void Validate(JwtOptions jwtOptions)
        {
            if (string.IsNullOrWhiteSpace(jwtOptions.Issuer) ||
                string.IsNullOrWhiteSpace(jwtOptions.Audience) ||
                string.IsNullOrWhiteSpace(jwtOptions.SecretKey) ||
                Encoding.UTF8.GetByteCount(jwtOptions.SecretKey) < 32)
            {
                throw new InvalidOperationException(
                    "JWT configuration is incomplete. Configure JWT:ISSUER, JWT:AUDIENCE, and JWT:SECRETKEY with at least 32 characters.");
            }
        }
    }
}
