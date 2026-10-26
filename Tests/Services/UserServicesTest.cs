using Domain.Const;
using Domain.Exceptions;
using Domain.Models;
using Infrastructure;
using Infrastructure.Interceptors;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using Services.Accessors.Interfaces;
using Services.Command.User;
using Services.Interfaces;
using Services.Services;
using Testcontainers.PostgreSql;
using Tests.Fakes;

namespace Tests.Services
{
    public class UserServicesTest
    {
        #region Configuration and Setup
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
               .AddInterceptors(new SoftDeleteInterceptor())
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
        #endregion

        #region GetUserProfileAsync
        [Test]
        public async Task GetUserProfileAsync_WhenTargetUserIdIsNull_ShouldReturnCurrentUserProfile()
        {
            // Arrange
            var currentUserId = Guid.NewGuid();
            var user = new User
            {
                Id = currentUserId,
                FirstName = "Jan",
                LastName = "Kowalski",
                Bio = "Moje bio",
                IsPublic = false
            };

            await _contextMock.Users.AddAsync(user);
            await _contextMock.SaveChangesAsync();

            // Act 
            var result = await _userServicesMock.GetUserProfileAsync(currentUserId, targetUserId: null);

            // Assert
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(result.Data!.UserId).IsEqualTo(currentUserId);
            await Assert.That(result.Data.FirstName).IsEqualTo("Jan");
        }

        [Test]
        public async Task GetUserProfileAsync_WhenTargetUserIsPrivateAndNotOwner_ShouldThrowForbiddenException()
        {
            // Arrange
            var ownerId = Guid.NewGuid();
            var requesterId = Guid.NewGuid();

            var privateUser = new User
            {
                Id = ownerId,
                FirstName = "Piotr",
                LastName = "Nowak",
                IsPublic = false
            };

            await _contextMock.Users.AddAsync(privateUser);
            await _contextMock.SaveChangesAsync();

            // Act & Assert
            await Assert.That(async () => await _userServicesMock.GetUserProfileAsync(requesterId, ownerId))
                .Throws<ForbiddenException>()
                .WithMessage("This profile is private.");
        }

        [Test]
        public async Task GetUserProfileAsync_WhenTargetUserIsPublic_ShouldReturnProfile()
        {
            // Arrange
            var ownerId = Guid.NewGuid();
            var requesterId = Guid.NewGuid();

            var publicUser = new User
            {
                Id = ownerId,
                FirstName = "Anna",
                LastName = "Zielińska",
                IsPublic = true
            };

            await _contextMock.Users.AddAsync(publicUser);
            await _contextMock.SaveChangesAsync();

            // Act
            var result = await _userServicesMock.GetUserProfileAsync(requesterId, ownerId);

            // Assert
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(result.Data!.FirstName).IsEqualTo("Anna");
        }

        [Test]
        public async Task GetUserProfileAsync_WhenUserDoesNotExist_ShouldThrowNotFoundException()
        {
            // Arrange
            var currentUserId = Guid.NewGuid();
            var nonExistentId = Guid.NewGuid();

            // Act & Assert
            await Assert.That(async () => await _userServicesMock.GetUserProfileAsync(currentUserId, nonExistentId))
                .Throws<NotFoundException>();
        }
        #endregion

        #region EditUserAsync
        [Test]
        public async Task EditUserAsync_WhenUserExists_ShouldUpdateAllProvidedFields()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var initialUser = new User
            {
                Id = userId,
                FirstName = "Jan",
                LastName = "Kowalski",
                Bio = "Stare bio"
            };

            await _contextMock.Users.AddAsync(initialUser);
            await _contextMock.SaveChangesAsync();

            var command = new EditUserCommand
            {
                FirstName = "Piotr",
                LastName = "Nowak",
                Bio = "Nowe zaktualizowane bio"
            };

            // Act
            var result = await _userServicesMock.EditUserAsync(userId, command);

            // Assert 
            await Assert.That(result).IsNotNull();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(result.Message).IsEqualTo("User data updated successfully.");

            var updatedUser = await _contextMock.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
            await Assert.That(updatedUser).IsNotNull();
            await Assert.That(updatedUser!.FirstName).IsEqualTo("Piotr");
            await Assert.That(updatedUser.LastName).IsEqualTo("Nowak");
            await Assert.That(updatedUser.Bio).IsEqualTo("Nowe zaktualizowane bio");
        }

        [Test]
        public async Task EditUserAsync_WhenOnlySomeFieldsProvided_ShouldOnlyUpdateProvidedFields()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var initialUser = new User
            {
                Id = userId,
                FirstName = "Jan",
                LastName = "Kowalski",
                Bio = "Oryginalne bio"
            };

            await _contextMock.Users.AddAsync(initialUser);
            await _contextMock.SaveChangesAsync();

            var command = new EditUserCommand
            {
                FirstName = "Tomasz",
                LastName = null,
                Bio = "Oryginalne bio"
            };

            // Act
            var result = await _userServicesMock.EditUserAsync(userId, command);

            // Assert
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);

            var updatedUser = await _contextMock.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
            await Assert.That(updatedUser).IsNotNull();
            await Assert.That(updatedUser!.FirstName).IsEqualTo("Tomasz");
            await Assert.That(updatedUser.LastName).IsEqualTo("Kowalski");
            await Assert.That(updatedUser.Bio).IsEqualTo("Oryginalne bio");
        }

        [Test]
        public async Task EditUserAsync_WhenBioIsEmptyOrWhitespace_ShouldSetBioToNull()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var initialUser = new User
            {
                Id = userId,
                FirstName = "Jan",
                LastName = "Kowalski",
                Bio = "Stare bio do usunięcia"
            };

            await _contextMock.Users.AddAsync(initialUser);
            await _contextMock.SaveChangesAsync();

            var command = new EditUserCommand
            {
                FirstName = null,
                LastName = null,
                Bio = "   "
            };

            // Act
            var result = await _userServicesMock.EditUserAsync(userId, command);

            // Assert
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);

            var updatedUser = await _contextMock.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
            await Assert.That(updatedUser).IsNotNull();
            await Assert.That(updatedUser!.Bio).IsNull();
            await Assert.That(updatedUser.FirstName).IsEqualTo("Jan");
            await Assert.That(updatedUser.LastName).IsEqualTo("Kowalski");
        }

        [Test]
        public async Task EditUserAsync_WhenUserDoesNotExist_ShouldThrowNotFoundException()
        {
            // Arrange
            var nonExistentUserId = Guid.NewGuid();
            var command = new EditUserCommand
            {
                FirstName = "Jan",
                LastName = "Kowalski",
                Bio = "Bio"
            };

            // Act & Assert
            await Assert.That(async () => await _userServicesMock.EditUserAsync(nonExistentUserId, command))
                .Throws<NotFoundException>()
                .WithMessage($"User with ID '{nonExistentUserId}' was not found.");
        }
        #endregion

        #region SetProfileVisibilityAsync
        [Test]
        public async Task SetProfileVisibilityAsync_WhenVisibilityChanges_ShouldUpdateFlagAndSaveToDb()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var user = new User
            {
                Id = userId,
                FirstName = "Jan",
                LastName = "Kowalski",
                IsPublic = false
            };

            await _contextMock.Users.AddAsync(user);
            await _contextMock.SaveChangesAsync();

            // Act
            var result = await _userServicesMock.SetProfileVisibilityAsync(userId, isPublic: true);

            // Assert
            await Assert.That(result).IsNotNull();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(result.Message).IsEqualTo("Profile visibility updated to public.");

            var updatedUser = await _contextMock.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
            await Assert.That(updatedUser).IsNotNull();
            await Assert.That(updatedUser!.IsPublic).IsTrue();
        }

        [Test]
        public async Task SetProfileVisibilityAsync_WhenVisibilityIsAlreadySet_ShouldReturnSuccessWithoutChangingState()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var user = new User
            {
                Id = userId,
                FirstName = "Jan",
                LastName = "Kowalski",
                IsPublic = true
            };

            await _contextMock.Users.AddAsync(user);
            await _contextMock.SaveChangesAsync();

            // Act
            var result = await _userServicesMock.SetProfileVisibilityAsync(userId, isPublic: true);

            // Assert
            await Assert.That(result).IsNotNull();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(result.Message).IsEqualTo("Profile visibility is already public.");

            var userInDb = await _contextMock.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
            await Assert.That(userInDb).IsNotNull();
            await Assert.That(userInDb!.IsPublic).IsTrue();
        }

        [Test]
        public async Task SetProfileVisibilityAsync_WhenUserDoesNotExist_ShouldThrowNotFoundException()
        {
            // Arrange
            var nonExistentUserId = Guid.NewGuid();

            // Act & Assert
            await Assert.That(async () => await _userServicesMock.SetProfileVisibilityAsync(nonExistentUserId, isPublic: true))
                .Throws<NotFoundException>()
                .WithMessage($"User with ID '{nonExistentUserId}' was not found.");
        }
        #endregion
    }
}
