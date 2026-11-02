using Api.Configuration;
using Domain.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Tests.Auth;

public class AuthenticationConfigurationTests
{
    [Test]
    public async Task AddAuthenticationConfiguration_WhenIdentityIsRegisteredFirst_UsesJwtBearerAsDefaultSchemes()
    {
        // Arrange
        using var provider = CreateProvider();
        var schemes = provider.GetRequiredService<IAuthenticationSchemeProvider>();

        // Act
        var authenticateScheme = await schemes.GetDefaultAuthenticateSchemeAsync();
        var challengeScheme = await schemes.GetDefaultChallengeSchemeAsync();
        var forbidScheme = await schemes.GetDefaultForbidSchemeAsync();

        // Assert
        await Assert.That(authenticateScheme!.Name).IsEqualTo(JwtBearerDefaults.AuthenticationScheme);
        await Assert.That(challengeScheme!.Name).IsEqualTo(JwtBearerDefaults.AuthenticationScheme);
        await Assert.That(forbidScheme!.Name).IsEqualTo(JwtBearerDefaults.AuthenticationScheme);
    }

    private static ServiceProvider CreateProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JWT:SecretKey"] = "test-secret-key-with-at-least-32-characters",
                ["JWT:Issuer"] = "MAP-API-Tests",
                ["JWT:Audience"] = "MAP-API-Tests"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();

        // Same order as Program.cs: AddInfrastructure (AddIdentity) runs before AddAuthenticationConfiguration.
        services.AddIdentity<User, IdentityRole<Guid>>();
        services.AddAuthenticationConfiguration(configuration);

        return services.BuildServiceProvider();
    }
}
