using Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Services.Options;
using System.Security.Claims;

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

            if (!JwtOptionsValidation.IsValid(jwtOptions))
            {
                throw new InvalidOperationException(JwtOptionsValidation.ErrorMessage);
            }

            // AddIdentity registers its cookie scheme as the default authenticate/challenge scheme,
            // so JWT bearer has to be set explicitly or bearer tokens are never read.
            services.AddAuthentication(options =>
                {
                    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
                    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                    options.DefaultForbidScheme = JwtBearerDefaults.AuthenticationScheme;
                })
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
                        IssuerSigningKey = jwtOptions.GetSigningKey(),
                        ClockSkew = TimeSpan.Zero
                    };

                    options.Events = new JwtBearerEvents
                    {
                        OnTokenValidated = ValidateUserIsActiveAsync
                    };
                });

            return services;
        }

        public static async Task ValidateUserIsActiveAsync(TokenValidatedContext context)
        {
            var userId = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!Guid.TryParse(userId, out var parsedUserId))
            {
                context.Fail("Token does not contain a valid user identifier.");
                return;
            }

            var dbContext = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();

            // The soft-delete query filter hides deleted accounts, so their unexpired access tokens stop working immediately.
            var isActive = await dbContext.Users
                .AsNoTracking()
                .AnyAsync(u => u.Id == parsedUserId, context.HttpContext.RequestAborted);

            if (!isActive)
            {
                context.Fail("User account no longer exists.");
            }
        }
    }
}
