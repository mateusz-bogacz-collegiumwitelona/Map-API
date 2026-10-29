using Domain.Common;
using Domain.Const;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

namespace Api.Configuration
{
    public static class RateLimitingConfiguration
    {
        public const string AuthPolicy = "Auth";

        public static IServiceCollection AddRateLimitingConfiguration(this IServiceCollection services)
        {
            services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

                options.AddPolicy(AuthPolicy, httpContext =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 10,
                            Window = TimeSpan.FromMinutes(1),
                            QueueLimit = 0,
                            AutoReplenishment = true
                        }));

                options.OnRejected = async (context, cancellationToken) =>
                {
                    context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

                    await context.HttpContext.Response.WriteAsJsonAsync(
                        Result.Failure(
                            message: "Too many requests. Please try again later.",
                            errorCode: ErrorCodes.Common.TooManyRequests,
                            statusCode: StatusCodes.Status429TooManyRequests),
                        cancellationToken);
                };
            });

            return services;
        }

        public static WebApplication UseRateLimitingConfiguration(this WebApplication app)
        {
            app.UseRateLimiter();
            return app;
        }
    }
}
