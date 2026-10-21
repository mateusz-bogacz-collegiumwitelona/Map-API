using Domain.Models;
using Infrastructure;
using Infrastructure.Seeders;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Tests.Seeders;

public class TestSeedData
{
    private static PostgreSqlContainer _dbContainer = null!;
    private static string _connectionString = null!;

    private ServiceProvider _serviceProvider = null!;
    private IServiceScope _scope = null!;
    private string _currentSchema = null!;

    protected AppDbContext Context => _scope.ServiceProvider.GetRequiredService<AppDbContext>();
    protected UserManager<User> UserManager => _scope.ServiceProvider.GetRequiredService<UserManager<User>>();
    protected RoleManager<IdentityRole<Guid>> RoleManager => _scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
    protected SeedData Sut => _scope.ServiceProvider.GetRequiredService<SeedData>();

    [Before(Class)]
    [Obsolete]
    public static async Task SetupClassAsync()
    {
        _dbContainer = new PostgreSqlBuilder()
            .WithImage("postgis/postgis:18-3.6")
            .WithDatabase("testdb")
            .WithUsername("testuser")
            .WithPassword("testpassword")
            .Build();

        await _dbContainer.StartAsync();
        _connectionString = _dbContainer.GetConnectionString();

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "CREATE EXTENSION IF NOT EXISTS unaccent;";
        await cmd.ExecuteNonQueryAsync();
    }

    [After(Class)]
    public static async Task CleanupClassAsync() => await _dbContainer.DisposeAsync();

    [Before(Test)]
    public async Task SetupAsync()
    {
        _currentSchema = "test_schema_" + Guid.NewGuid().ToString("N");

        await using (var conn = new NpgsqlConnection(_connectionString))
        {
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"CREATE SCHEMA IF NOT EXISTS {_currentSchema};";
            await cmd.ExecuteNonQueryAsync();
        }

        var schemaConnectionString = $"{_connectionString};SearchPath={_currentSchema},public;Pooling=false;";

        var services = new ServiceCollection();
        
        services.AddLogging();

        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseNpgsql(schemaConnectionString, npgsqlOptions =>
            {
                npgsqlOptions.UseNetTopologySuite();
                npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", _currentSchema);
            });
        });

        services.AddIdentity<User, IdentityRole<Guid>>(options =>
        {
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequiredLength = 8;
            options.Password.RequiredUniqueChars = 1;
        })
        .AddEntityFrameworkStores<AppDbContext>()
        .AddDefaultTokenProviders();

        services.AddScoped<SeedData>();

        _serviceProvider = services.BuildServiceProvider();
        _scope = _serviceProvider.CreateScope();

        var createScript = Context.Database.GenerateCreateScript();
        await Context.Database.ExecuteSqlRawAsync(createScript);
    }

    [After(Test)]
    public async Task TearDownAsync()
    {
        _scope.Dispose();
        await _serviceProvider.DisposeAsync();
    }

    [Test]
    public async Task InitAsync_WhenDatabaseIsEmpty_ShouldSeedRolesAndUsersSuccessfully()
    {
        // Act
        await Sut.InitAsync();

        // Assert
        var adminRoleExists = await RoleManager.RoleExistsAsync("Admin");
        var userRoleExists = await RoleManager.RoleExistsAsync("User");

        await Assert.That(adminRoleExists).IsTrue();
        await Assert.That(userRoleExists).IsTrue();

        var usersCount = await UserManager.Users.CountAsync();
        await Assert.That(usersCount).IsEqualTo(5);

        var publicUser = await UserManager.FindByEmailAsync("public@example.pl");
        await Assert.That(publicUser).IsNotNull();
        await Assert.That(publicUser!.UserName).IsEqualTo("public@example.pl");
        await Assert.That(publicUser.FirstName).IsEqualTo("Adam");
        await Assert.That(publicUser.LastName).IsEqualTo("Kowalki");

        var isInRole = await UserManager.IsInRoleAsync(publicUser, "User");
        await Assert.That(isInRole).IsTrue();
    }

    [Test]
    public async Task InitAsync_WhenInvokedMultipleTimes_ShouldBeIdempotentAndNotThrowOrDuplicate()
    {
        // Arrange
        await Sut.InitAsync();

        // Act 
        await Sut.InitAsync();

        // Assert 
        var rolesCount = await RoleManager.Roles.CountAsync();
        var usersCount = await UserManager.Users.CountAsync();

        await Assert.That(rolesCount).IsEqualTo(2);
        await Assert.That(usersCount).IsEqualTo(5);
    }
}