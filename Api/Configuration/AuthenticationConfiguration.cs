using Domain.Common;
using Domain.Const;
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
        private const string AccountBannedItemKey = "AccountBanned";

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
                        OnTokenValidated = ValidateUserIsActiveAsync,
                        OnChallenge = HandleChallengeAsync
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

            var now = DateTime.UtcNow;
            var userStatus = await dbContext.Users
                .AsNoTracking()
                .Where(u => u.Id == parsedUserId)
                .Select(u => new
                {
                    u.IsBanned,
                    u.BannedUntil
                })
                .FirstOrDefaultAsync(context.HttpContext.RequestAborted);

            if (userStatus is null)
            {
                context.Fail("User account no longer exists.");
                return;
            }

            if (userStatus.IsBanned)
            {
                if (userStatus.BannedUntil is not null && userStatus.BannedUntil <= now)
                {
                    await dbContext.Users
                        .Where(u => u.Id == parsedUserId && u.IsBanned)
                        .ExecuteUpdateAsync(setters => setters
                            .SetProperty(u => u.IsBanned, false)
                            .SetProperty(u => u.BannedUntil, (DateTime?)null)
                            .SetProperty(u => u.BannedReason, (string?)null),
                            context.HttpContext.RequestAborted);

                    return;
                }

                context.HttpContext.Items[AccountBannedItemKey] = true;
                context.Fail("User account is banned.");
            }
        }

        public static async Task HandleChallengeAsync(JwtBearerChallengeContext context)
        {
            if (context.HttpContext.Items.ContainsKey(AccountBannedItemKey))
            {
                context.HandleResponse();
                context.Response.StatusCode = StatusCodes.Status403Forbidden;

                await context.Response.WriteAsJsonAsync(
                    Result.Failure(
                        message: "Account is banned.",
                        errorCode: ErrorCodes.AccountBanned,
                        statusCode: StatusCodes.Status403Forbidden),
                    context.HttpContext.RequestAborted);
            }
        }
    }
}