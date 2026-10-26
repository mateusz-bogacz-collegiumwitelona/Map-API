using Domain.Const;
using Domain.Models;
using Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.CodeCoverage.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using Services.Accessors.Interfaces;
using Services.Interfaces;
using Services.Services;
using Testcontainers.PostgreSql;
using Tests.Fakes;
using TUnit.Core.Interfaces;

namespace Tests.Services
{
    public class UserServicesTest
    {
        protected AppDbContext _contextMock = null!;

        private static PostgreSqlContainer _dbContainer = null!;
        private static string _connectionString = null!;

        protected IUserServices _userServicesMock = null!;
        protected ILogger<UserServices> _loggerMock = null!;
        private string _currentSchema = null!;
        private ICancellationTokenAccessor _ctMock = null!;

        [Before(Class)]
        [Obsolete]
        public static async Task SetupClassAsync()
        {
            _dbContainer = new PostgreSqlBuilder()
                .WithImage("postgis/postgis:18-3.6")
                .WithDatabase("testdb")
                .WithUsername("testuser")
                .WithPassword("testpassword")
                .WithCommand(
                    "-c", "max_connections=300",
                    "-c", "max_locks_per_transaction=1024",
                    "-c", "shared_buffers=256MB"
                )
                .Build();

            await _dbContainer.StartAsync();

            _connectionString = _dbContainer.GetConnectionString();

            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "CREATE EXTENSION IF NOT EXISTS unaccent;";
            await cmd.ExecuteNonQueryAsync();
        }

        [After(Class)]
        public static async Task CleanupClassAsync()
           => await _dbContainer.DisposeAsync();

        [Before(Test)]
        public async Task SetupAsync()
        {
            _currentSchema = "test_schema_" + Guid.NewGuid().ToString("N");

            using (var conn = new NpgsqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = $"CREATE SCHEMA IF NOT EXISTS {_currentSchema};";
                await cmd.ExecuteNonQueryAsync();
            }

            var schemaConnectionString = $"{_connectionString};SearchPath={_currentSchema},public";

            var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(schemaConnectionString, options =>
                {
                    options.UseNetTopologySuite();
                    options.MigrationsHistoryTable("__EFMigrationsHistory", _currentSchema);
                })
               .LogTo(Console.WriteLine, LogLevel.Warning)
               .EnableSensitiveDataLogging()
               .EnableDetailedErrors()
               .Options;

            _contextMock = new AppDbContext(dbOptions);

            var createScript = _contextMock.Database.GenerateCreateScript();
            await _contextMock.Database.ExecuteSqlRawAsync(createScript);

            _loggerMock = new LoggerFactory().CreateLogger<UserServices>();


            _ctMock = new FakeCancellationTokenAccessor();

            _userServicesMock = new UserServices(_contextMock, _loggerMock, _ctMock);
        }

        [After(Test)]
        public async Task CleanupAsync()
        {
            await _contextMock.DisposeAsync();

            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"DROP SCHEMA IF EXISTS {_currentSchema} CASCADE;";
            await cmd.ExecuteNonQueryAsync();
        }

        [Test]
        public async Task GetUserDataAsync_WhenUserExists_ShouldReturnSuccessResult()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var user = new User
            {
                Id = userId,
                FirstName = "Jan",
                LastName = "Kowalski",
                Bio = "Przykładowe bio"
            };

            await _contextMock.Users.AddAsync(user);
            await _contextMock.SaveChangesAsync();

            // Act
            var result = await _userServicesMock.GetUserDataAsync(userId);

            // Assert
            await Assert.That(result).IsNotNull();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(result.Message).IsEqualTo("User data retrieved successfully.");
            await Assert.That(result.Data).IsNotNull();
            await Assert.That(result.Data!.UserId).IsEqualTo(userId);
            await Assert.That(result.Data.FirstName).IsEqualTo("Jan");
            await Assert.That(result.Data.LastName).IsEqualTo("Kowalski");
            await Assert.That(result.Data.Bio).IsEqualTo("Przykładowe bio");
        }

        [Test]
        public async Task GetUserDataAsync_WhenUserDoesNotExist_ShouldReturnNotFoundResult()
        {
            // Arrange
            var nonExistentUserId = Guid.NewGuid();

            // Act
            var result = await _userServicesMock.GetUserDataAsync(nonExistentUserId);

            // Assert
            await Assert.That(result).IsNotNull();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status404NotFound);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.NotFound);
            await Assert.That(result.Message).IsEqualTo("User not found.");
            await Assert.That(result.Data).IsNull();
        }
    }
}
