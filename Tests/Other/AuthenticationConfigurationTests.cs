using Api.Configuration;
using Domain.Common;
using Domain.Const;
using Domain.Models;
using Infrastructure;
using Infrastructure.Interceptors;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using System.Security.Claims;
using System.Text.Json;
using Testcontainers.PostgreSql;

namespace Tests.Other
{
    public class AuthenticationConfigurationTests
    {
        #region Configuration and Setup
        private AppDbContext _context = null!;
        private static PostgreSqlContainer _dbContainer = null!;
        private static string _connectionString = null!;
        private string _currentSchema = null!;

        [Before(Class)]
        public static async Task SetupClassAsync()
        {
            _dbContainer = new PostgreSqlBuilder("postgis/postgis:18-3.6")
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
                .AddInterceptors(new SoftDeleteInterceptor())
                .LogTo(Console.WriteLine, LogLevel.Warning)
                .EnableSensitiveDataLogging()
                .EnableDetailedErrors()
                .Options;

            _context = new AppDbContext(dbOptions);

            var createScript = _context.Database.GenerateCreateScript();
            await _context.Database.ExecuteSqlRawAsync(createScript);
        }

        [After(Test)]
        public async Task CleanupAsync()
        {
            await _context.DisposeAsync();

            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"DROP SCHEMA IF EXISTS {_currentSchema} CASCADE;";
            await cmd.ExecuteNonQueryAsync();
        }

        private TokenValidatedContext CreateTokenValidatedContext(string? rawUserId, bool includePrincipal = true)
        {
            var services = new ServiceCollection();
            services.AddSingleton(_context);
            var serviceProvider = services.BuildServiceProvider();

            var httpContext = new DefaultHttpContext
            {
                RequestServices = serviceProvider
            };

            ClaimsPrincipal? principal = null;
            if (includePrincipal)
            {
                var claims = new List<Claim>();
                if (rawUserId is not null)
                {
                    claims.Add(new Claim(ClaimTypes.NameIdentifier, rawUserId));
                }

                var identity = new ClaimsIdentity(claims, JwtBearerDefaults.AuthenticationScheme);
                principal = new ClaimsPrincipal(identity);
            }

            var scheme = new AuthenticationScheme(
                JwtBearerDefaults.AuthenticationScheme,
                JwtBearerDefaults.AuthenticationScheme,
                typeof(JwtBearerHandler));

            return new TokenValidatedContext(httpContext, scheme, new JwtBearerOptions())
            {
                Principal = principal
            };
        }

        private static JwtBearerChallengeContext CreateChallengeContext(DefaultHttpContext httpContext)
        {
            var scheme = new AuthenticationScheme(
                JwtBearerDefaults.AuthenticationScheme,
                JwtBearerDefaults.AuthenticationScheme,
                typeof(JwtBearerHandler));

            var properties = new AuthenticationProperties();
            return new JwtBearerChallengeContext(httpContext, scheme, new JwtBearerOptions(), properties);
        }

        private async Task<User> AddUserAsync(
            bool isBanned = false,
            DateTime? bannedUntil = null,
            string? bannedReason = null)
        {
            var id = Guid.NewGuid();
            var user = new User
            {
                Id = id,
                UserName = $"user_{id:N}",
                NormalizedUserName = $"USER_{id:N}",
                Email = $"user_{id:N}@example.com",
                NormalizedEmail = $"USER_{id:N}@EXAMPLE.COM",
                SecurityStamp = Guid.NewGuid().ToString(),
                FirstName = "Jan",
                LastName = "Kowalski",
                IsBanned = isBanned,
                BannedUntil = bannedUntil,
                BannedReason = bannedReason
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return user;
        }
        #endregion

        #region ValidateUserIsActiveAsync Tests

        [Test]
        public async Task ValidateUserIsActiveAsync_WhenUserIsActiveAndNotBanned_ShouldSucceed()
        {
            // Arrange
            var user = await AddUserAsync(isBanned: false);
            var context = CreateTokenValidatedContext(user.Id.ToString());

            // Act
            await AuthenticationConfiguration.ValidateUserIsActiveAsync(context);

            // Assert
            await Assert.That(context.Result).IsNull();
            await Assert.That(context.HttpContext.Items.ContainsKey("AccountBanned")).IsFalse();
        }

        [Test]
        public async Task ValidateUserIsActiveAsync_WhenTemporaryBanHasExpired_ShouldSucceedAndClearBanInDatabase()
        {
            // Arrange
            var user = await AddUserAsync(
                isBanned: true,
                bannedUntil: DateTime.UtcNow.AddMinutes(-1),
                bannedReason: "Temporary ban");
            var context = CreateTokenValidatedContext(user.Id.ToString());

            // Act
            await AuthenticationConfiguration.ValidateUserIsActiveAsync(context);

            // Assert
            await Assert.That(context.Result).IsNull();
            await Assert.That(context.HttpContext.Items.ContainsKey("AccountBanned")).IsFalse();

            var updatedUser = await _context.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id);
            await Assert.That(updatedUser.IsBanned).IsFalse();
            await Assert.That(updatedUser.BannedUntil).IsNull();
            await Assert.That(updatedUser.BannedReason).IsNull();
        }

        [Test]
        public async Task ValidateUserIsActiveAsync_WhenUserHasActiveTemporaryBan_ShouldFailAndSetBannedItem()
        {
            // Arrange
            var user = await AddUserAsync(
                isBanned: true,
                bannedUntil: DateTime.UtcNow.AddDays(1),
                bannedReason: "Active ban");
            var context = CreateTokenValidatedContext(user.Id.ToString());

            // Act
            await AuthenticationConfiguration.ValidateUserIsActiveAsync(context);

            // Assert
            await Assert.That(context.Result).IsNotNull();
            await Assert.That(context.Result!.Failure).IsNotNull();
            await Assert.That(context.Result.Failure!.Message).IsEqualTo("User account is banned.");
            await Assert.That(context.HttpContext.Items.ContainsKey("AccountBanned")).IsTrue();
        }

        [Test]
        public async Task ValidateUserIsActiveAsync_WhenUserHasPermanentBan_ShouldFailAndSetBannedItem()
        {
            // Arrange
            var user = await AddUserAsync(
                isBanned: true,
                bannedUntil: null,
                bannedReason: "Permanent ban");
            var context = CreateTokenValidatedContext(user.Id.ToString());

            // Act
            await AuthenticationConfiguration.ValidateUserIsActiveAsync(context);

            // Assert
            await Assert.That(context.Result).IsNotNull();
            await Assert.That(context.Result!.Failure).IsNotNull();
            await Assert.That(context.Result.Failure!.Message).IsEqualTo("User account is banned.");
            await Assert.That(context.HttpContext.Items.ContainsKey("AccountBanned")).IsTrue();
        }

        [Test]
        public async Task ValidateUserIsActiveAsync_WhenUserDoesNotExist_ShouldFailWithoutSettingBannedItem()
        {
            // Arrange
            var context = CreateTokenValidatedContext(Guid.NewGuid().ToString());

            // Act
            await AuthenticationConfiguration.ValidateUserIsActiveAsync(context);

            // Assert
            await Assert.That(context.Result).IsNotNull();
            await Assert.That(context.Result!.Failure!.Message).IsEqualTo("User account no longer exists.");
            await Assert.That(context.HttpContext.Items.ContainsKey("AccountBanned")).IsFalse();
        }

        [Test]
        [Arguments(null)]
        [Arguments("")]
        [Arguments("not-a-valid-guid")]
        public async Task ValidateUserIsActiveAsync_WhenUserIdClaimIsMissingOrInvalid_ShouldFail(string? invalidUserId)
        {
            // Arrange
            var context = CreateTokenValidatedContext(invalidUserId);

            // Act
            await AuthenticationConfiguration.ValidateUserIsActiveAsync(context);

            // Assert
            await Assert.That(context.Result).IsNotNull();
            await Assert.That(context.Result!.Failure!.Message).IsEqualTo("Token does not contain a valid user identifier.");
            await Assert.That(context.HttpContext.Items.ContainsKey("AccountBanned")).IsFalse();
        }

        [Test]
        public async Task ValidateUserIsActiveAsync_WhenPrincipalIsNull_ShouldFail()
        {
            // Arrange
            var context = CreateTokenValidatedContext(rawUserId: null, includePrincipal: false);

            // Act
            await AuthenticationConfiguration.ValidateUserIsActiveAsync(context);

            // Assert
            await Assert.That(context.Result).IsNotNull();
            await Assert.That(context.Result!.Failure!.Message).IsEqualTo("Token does not contain a valid user identifier.");
            await Assert.That(context.HttpContext.Items.ContainsKey("AccountBanned")).IsFalse();
        }

        #endregion

        #region HandleChallengeAsync Tests

        [Test]
        public async Task HandleChallengeAsync_WhenAccountBannedItemIsPresent_ShouldHandleResponseAndWrite403FailureJson()
        {
            // Arrange
            var httpContext = new DefaultHttpContext();
            httpContext.Response.Body = new MemoryStream();
            httpContext.Items["AccountBanned"] = true;

            var context = CreateChallengeContext(httpContext);

            // Act
            await AuthenticationConfiguration.HandleChallengeAsync(context);

            // Assert
            await Assert.That(context.Handled).IsTrue();
            await Assert.That(httpContext.Response.StatusCode).IsEqualTo(StatusCodes.Status403Forbidden);

            httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
            var body = await new StreamReader(httpContext.Response.Body).ReadToEndAsync();
            var result = JsonSerializer.Deserialize<Result>(body, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            await Assert.That(result).IsNotNull();
            await Assert.That(result!.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status403Forbidden);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.AccountBanned);
            await Assert.That(result.Message).IsEqualTo("Account is banned.");
        }

        [Test]
        public async Task HandleChallengeAsync_WhenAccountBannedItemIsNotPresent_ShouldNotHandleResponse()
        {
            // Arrange
            var httpContext = new DefaultHttpContext();
            var context = CreateChallengeContext(httpContext);

            // Act
            await AuthenticationConfiguration.HandleChallengeAsync(context);

            // Assert
            await Assert.That(context.Handled).IsFalse();
        }

        #endregion

        #region AddAuthenticationConfiguration Tests

        [Test]
        public async Task AddAuthenticationConfiguration_WhenJwtSectionIsMissing_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var services = new ServiceCollection();
            var configuration = new ConfigurationBuilder().Build();

            // Act & Assert
            await Assert.That(() => services.AddAuthenticationConfiguration(configuration))
                .Throws<InvalidOperationException>()
                .WithMessage("JWT configuration section is missing.");
        }

        [Test]
        public async Task AddAuthenticationConfiguration_WhenJwtOptionsAreInvalid_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var services = new ServiceCollection();
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Jwt:Issuer"] = "",
                    ["Jwt:Audience"] = "",
                    ["Jwt:SecretKey"] = "short"
                })
                .Build();

            // Act & Assert
            await Assert.That(() => services.AddAuthenticationConfiguration(configuration))
                .Throws<InvalidOperationException>();
        }

        [Test]
        public async Task AddAuthenticationConfiguration_WhenConfigurationIsValid_ShouldRegisterJwtAuthentication()
        {
            // Arrange
            var services = new ServiceCollection();
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Jwt:Issuer"] = "TestIssuer",
                    ["Jwt:Audience"] = "TestAudience",
                    ["Jwt:SecretKey"] = "SuperSecretKeyForTestingPurposesThatIsLongEnough123!"
                })
                .Build();

            // Act
            services.AddAuthenticationConfiguration(configuration);
            var provider = services.BuildServiceProvider();
            var authOptions = provider.GetRequiredService<IOptions<AuthenticationOptions>>().Value;

            // Assert
            await Assert.That(authOptions.DefaultAuthenticateScheme).IsEqualTo(JwtBearerDefaults.AuthenticationScheme);
            await Assert.That(authOptions.DefaultChallengeScheme).IsEqualTo(JwtBearerDefaults.AuthenticationScheme);
            await Assert.That(authOptions.DefaultScheme).IsEqualTo(JwtBearerDefaults.AuthenticationScheme);
            await Assert.That(authOptions.DefaultForbidScheme).IsEqualTo(JwtBearerDefaults.AuthenticationScheme);
        }

        #endregion
    }
}