using Api.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;

namespace Tests.Auth;

public class AuthorizationConfigurationTests
{
    [Test]
    public async Task UserPolicy_AllowsUserRoleAndRejectsAdminRole()
    {
        // Arrange
        using var provider = CreateProvider();
        var authorization = provider.GetRequiredService<IAuthorizationService>();

        // Act
        var userResult = await authorization.AuthorizeAsync(
            CreatePrincipal("User"),
            resource: null,
            AuthorizationConfiguration.UserPolicy);

        var adminResult = await authorization.AuthorizeAsync(
            CreatePrincipal("Admin"),
            resource: null,
            AuthorizationConfiguration.UserPolicy);

        // Assert
        await Assert.That(userResult.Succeeded).IsTrue();
        await Assert.That(adminResult.Succeeded).IsFalse();
    }

    [Test]
    public async Task AdminPolicy_AllowsAdminRoleAndRejectsUserRole()
    {
        // Arrange
        using var provider = CreateProvider();
        var authorization = provider.GetRequiredService<IAuthorizationService>();

        // Act
        var adminResult = await authorization.AuthorizeAsync(
            CreatePrincipal("Admin"),
            resource: null,
            AuthorizationConfiguration.AdminPolicy);

        var userResult = await authorization.AuthorizeAsync(
            CreatePrincipal("User"),
            resource: null,
            AuthorizationConfiguration.AdminPolicy);

        // Assert
        await Assert.That(adminResult.Succeeded).IsTrue();
        await Assert.That(userResult.Succeeded).IsFalse();
    }

    [Test]
    public async Task Policies_RejectUnauthenticatedPrincipal()
    {
        // Arrange
        using var provider = CreateProvider();
        var authorization = provider.GetRequiredService<IAuthorizationService>();
        var anonymous = new ClaimsPrincipal(new ClaimsIdentity());

        // Act
        var userResult = await authorization.AuthorizeAsync(
            anonymous,
            resource: null,
            AuthorizationConfiguration.UserPolicy);

        var adminResult = await authorization.AuthorizeAsync(
            anonymous,
            resource: null,
            AuthorizationConfiguration.AdminPolicy);

        // Assert
        await Assert.That(userResult.Succeeded).IsFalse();
        await Assert.That(adminResult.Succeeded).IsFalse();
    }

    private static ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorizationConfiguration();

        return services.BuildServiceProvider();
    }

    private static ClaimsPrincipal CreatePrincipal(string role)
        => new(new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.Role, role) },
            authenticationType: "Test"));
}
