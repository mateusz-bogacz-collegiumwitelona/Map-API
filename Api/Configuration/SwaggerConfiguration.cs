using Api.Transformers;
using Microsoft.OpenApi;

namespace Api.Configuration
{
    public static class SwaggerConfiguration
    {
        public static IServiceCollection AddSwaggerConfiguration(this IServiceCollection services)
        {
            services.AddOpenApi(options =>
            {
                options.AddOperationTransformer<LoginRequestExamplesTransformer>();

                options.AddDocumentTransformer(static (document, context, cancellationToken) =>
                {
                    document.Components ??= new OpenApiComponents();
                    document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();

                    var scheme = new OpenApiSecurityScheme
                    {
                        Type = SecuritySchemeType.Http,
                        Scheme = "bearer",
                        BearerFormat = "JWT",
                        Description = "Wklej tutaj swój token JWT (sam token, bez słowa Bearer)."
                    };

                    document.Components.SecuritySchemes["Bearer"] = scheme;

                    document.Security ??= new List<OpenApiSecurityRequirement>();

                    var requirement = new OpenApiSecurityRequirement
                    {
                        [new OpenApiSecuritySchemeReference("Bearer", document)] = new List<string>()
                    };

                    document.Security.Add(requirement);

                    return Task.CompletedTask;
                });
            });

            return services;
        }

        public static WebApplication UseSwaggerUIConfiguration(this WebApplication app)
        {
            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
                app.UseSwaggerUI(options =>
                {
                    options.SwaggerEndpoint("/openapi/v1.json", "MAP API V1");
                    options.ConfigObject.PersistAuthorization = true;
                });
            }

            return app;
        }
    }
}