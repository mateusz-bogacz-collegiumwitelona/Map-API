using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using System.Text.Json.Nodes;

namespace Api.Transformers
{
    public class LoginRequestExamplesTransformer : IOpenApiOperationTransformer
    {
        private readonly IConfiguration _configuration;
        private readonly IHostEnvironment _environment;

        public LoginRequestExamplesTransformer(
            IConfiguration configuration,
            IHostEnvironment environment)
        {
            _configuration = configuration;
            _environment = environment;
        }

        public Task TransformAsync(
            OpenApiOperation operation,
            OpenApiOperationTransformerContext context,
            CancellationToken cancellationToken)
        {
            if (_environment.IsProduction())
            {
                return Task.CompletedTask;
            }

            if (context.Description.ActionDescriptor is ControllerActionDescriptor actionDescriptor &&
                (actionDescriptor.ActionName == "Login" || actionDescriptor.ActionName == "LoginAsync"))
            {
                if (operation?.RequestBody?.Content != null &&
                    operation.RequestBody.Content.TryGetValue("application/json", out var content) &&
                    content != null)
                {
                    var examples = new Dictionary<string, IOpenApiExample>();

                    var adminEmail = _configuration["SEED_ADMIN_EMAIL"];
                    var adminPassword = _configuration["SEED_ADMIN_PASSWORD"];

                    if (!string.IsNullOrWhiteSpace(adminEmail) && !string.IsNullOrWhiteSpace(adminPassword))
                    {
                        examples["Admin"] = new OpenApiExample
                        {
                            Summary = "Admin account (Tomasz Kot)",
                            Value = new JsonObject
                            {
                                ["email"] = adminEmail,
                                ["password"] = adminPassword
                            }
                        };
                    }

                    examples["PublicUser"] = new OpenApiExample
                    {
                        Summary = "Public User account (Adam Kowalski)",
                        Value = new JsonObject
                        {
                            ["email"] = "public@example.pl",
                            ["password"] = "Public123!"
                        }
                    };

                    examples["PrivateUser"] = new OpenApiExample
                    {
                        Summary = "Private User account (Jan Urban)",
                        Value = new JsonObject
                        {
                            ["email"] = "private@example.pl",
                            ["password"] = "Private123!"
                        }
                    };

                    examples["TestUser0"] = new OpenApiExample
                    {
                        Summary = "Test User 0 (seeded with sample trips)",
                        Value = new JsonObject
                        {
                            ["email"] = "Test0@example.pl",
                            ["password"] = "TestUser0!123"
                        }
                    };

                    content.Examples = examples;
                }
            }

            return Task.CompletedTask;
        }
    }
}