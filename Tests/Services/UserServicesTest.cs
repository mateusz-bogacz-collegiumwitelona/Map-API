using Domain.Exceptions;
using Domain.Models;
using Infrastructure;
using Infrastructure.Interceptors;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using Services.Accessors.Interfaces;
using Services.Command.List;
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
        public async Task GetUserProfileAsync_WhenTargetUserIsPrivateAndNotOwner_ShouldThrowNotFoundException()
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
                .Throws<NotFoundException>()
                .WithMessage($"User with ID '{ownerId}' was not found.");
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

        #region GetUserListAsync
        [Test]
        public async Task GetUserListAsync_WhenDefaultFlagsUsed_ShouldExcludeBannedAndAdminUsers()
        {
            // Arrange
            var adminRole = new Microsoft.AspNetCore.Identity.IdentityRole<Guid> { Id = Guid.NewGuid(), Name = "Admin", NormalizedName = "ADMIN" };
            var userRole = new Microsoft.AspNetCore.Identity.IdentityRole<Guid> { Id = Guid.NewGuid(), Name = "User", NormalizedName = "USER" };
            await _contextMock.Roles.AddRangeAsync(adminRole, userRole);

            var regularUser = new User { Id = Guid.NewGuid(), FirstName = "Jan", LastName = "Kowalski", Email = "jan@test.pl", IsBanned = false };
            var adminUser = new User { Id = Guid.NewGuid(), FirstName = "Adam", LastName = "Adminowski", Email = "admin@test.pl", IsBanned = false };
            var bannedUser = new User { Id = Guid.NewGuid(), FirstName = "Marek", LastName = "Zbanowany", Email = "banned@test.pl", IsBanned = true, BannedUntil = DateTime.UtcNow.AddDays(7) };
            var noRoleUser = new User { Id = Guid.NewGuid(), FirstName = "Ewa", LastName = "Nowak", Email = "ewa@test.pl", IsBanned = false };

            await _contextMock.Users.AddRangeAsync(regularUser, adminUser, bannedUser, noRoleUser);
            await _contextMock.UserRoles.AddRangeAsync(
                new Microsoft.AspNetCore.Identity.IdentityUserRole<Guid> { UserId = regularUser.Id, RoleId = userRole.Id },
                new Microsoft.AspNetCore.Identity.IdentityUserRole<Guid> { UserId = adminUser.Id, RoleId = adminRole.Id },
                new Microsoft.AspNetCore.Identity.IdentityUserRole<Guid> { UserId = bannedUser.Id, RoleId = userRole.Id }
            );
            await _contextMock.SaveChangesAsync();

            var command = new UserListCommand
            {
                PageNumber = 1,
                PageSize = 10,
                IncludeBanned = false,
                IncludeAdmins = false
            };

            // Act
            var result = await _userServicesMock.GetUserListAsnyc(command);

            // Assert
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(result.Message).IsEqualTo("UserList retrieved successfully.");
            await Assert.That(result.Data).IsNotNull();
            await Assert.That(result.Data!.TotalCount).IsEqualTo(2);
            await Assert.That(result.Data.Items.Count).IsEqualTo(2);

            var janDto = result.Data.Items.First(u => u.Id == regularUser.Id);
            await Assert.That(janDto.Role).IsEqualTo("User");
            await Assert.That(janDto.IsBanned).IsFalse();

            var ewaDto = result.Data.Items.First(u => u.Id == noRoleUser.Id);
            await Assert.That(ewaDto.Role).IsEqualTo(string.Empty);
        }

        [Test]
        public async Task GetUserListAsync_WhenIncludeBannedIsTrue_ShouldReturnBannedUsers()
        {
            // Arrange
            var bannedUntil = DateTime.UtcNow.AddDays(5);
            var activeUser = new User { Id = Guid.NewGuid(), FirstName = "Jan", LastName = "Aktywny", IsBanned = false };
            var bannedUser = new User { Id = Guid.NewGuid(), FirstName = "Piotr", LastName = "Zbanowany", IsBanned = true, BannedUntil = bannedUntil };

            await _contextMock.Users.AddRangeAsync(activeUser, bannedUser);
            await _contextMock.SaveChangesAsync();

            var command = new UserListCommand
            {
                PageNumber = 1,
                PageSize = 10,
                IncludeBanned = true,
                IncludeAdmins = false
            };

            // Act
            var result = await _userServicesMock.GetUserListAsnyc(command);

            // Assert
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(result.Data!.TotalCount).IsEqualTo(2);

            var bannedDto = result.Data.Items.First(u => u.Id == bannedUser.Id);
            await Assert.That(bannedDto.IsBanned).IsTrue();
            await Assert.That(bannedDto.BannedUntil).IsNotNull();
        }

        [Test]
        public async Task GetUserListAsync_WhenIncludeAdminsIsTrue_ShouldReturnAdminUsers()
        {
            // Arrange
            var adminRole = new Microsoft.AspNetCore.Identity.IdentityRole<Guid> { Id = Guid.NewGuid(), Name = "Admin", NormalizedName = "ADMIN" };
            await _contextMock.Roles.AddAsync(adminRole);

            var regularUser = new User { Id = Guid.NewGuid(), FirstName = "Jan", LastName = "Kowalski" };
            var adminUser = new User { Id = Guid.NewGuid(), FirstName = "Anna", LastName = "Administrator" };

            await _contextMock.Users.AddRangeAsync(regularUser, adminUser);
            await _contextMock.UserRoles.AddAsync(new Microsoft.AspNetCore.Identity.IdentityUserRole<Guid>
            {
                UserId = adminUser.Id,
                RoleId = adminRole.Id
            });
            await _contextMock.SaveChangesAsync();

            var command = new UserListCommand
            {
                PageNumber = 1,
                PageSize = 10,
                IncludeBanned = false,
                IncludeAdmins = true
            };

            // Act
            var result = await _userServicesMock.GetUserListAsnyc(command);

            // Assert
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(result.Data!.TotalCount).IsEqualTo(2);

            var adminDto = result.Data.Items.First(u => u.Id == adminUser.Id);
            await Assert.That(adminDto.Role).IsEqualTo("Admin");
        }

        [Test]
        public async Task GetUserListAsync_WhenSearchTermHasDiacriticsOrMixedCase_ShouldFilterCaseAndAccentInsensitively()
        {
            // Arrange
            var moderatorRole = new Microsoft.AspNetCore.Identity.IdentityRole<Guid> { Id = Guid.NewGuid(), Name = "Moderator", NormalizedName = "MODERATOR" };
            await _contextMock.Roles.AddAsync(moderatorRole);

            var user1 = new User { Id = Guid.NewGuid(), FirstName = "Łukasz", LastName = "Żółć", Email = "lukasz@example.com" };
            var user2 = new User { Id = Guid.NewGuid(), FirstName = "Katarzyna", LastName = "Nowak", Email = "kasia@firma.pl" };
            var user3 = new User { Id = Guid.NewGuid(), FirstName = "Tomasz", LastName = "Zieliński", Email = "tomek@example.com" };

            await _contextMock.Users.AddRangeAsync(user1, user2, user3);
            await _contextMock.UserRoles.AddAsync(new Microsoft.AspNetCore.Identity.IdentityUserRole<Guid>
            {
                UserId = user3.Id,
                RoleId = moderatorRole.Id
            });
            await _contextMock.SaveChangesAsync();

            // Act 
            var resultByName = await _userServicesMock.GetUserListAsnyc(new UserListCommand
            {
                SearchTerm = "lukasz zolc",
                PageNumber = 1,
                PageSize = 10
            });

            var resultByEmail = await _userServicesMock.GetUserListAsnyc(new UserListCommand
            {
                SearchTerm = "FIRMA.PL",
                PageNumber = 1,
                PageSize = 10
            });

            var resultByRole = await _userServicesMock.GetUserListAsnyc(new UserListCommand
            {
                SearchTerm = "moder",
                PageNumber = 1,
                PageSize = 10
            });

            // Assert
            await Assert.That(resultByName.Data!.TotalCount).IsEqualTo(1);
            await Assert.That(resultByName.Data.Items[0].Id).IsEqualTo(user1.Id);

            await Assert.That(resultByEmail.Data!.TotalCount).IsEqualTo(1);
            await Assert.That(resultByEmail.Data.Items[0].Id).IsEqualTo(user2.Id);

            await Assert.That(resultByRole.Data!.TotalCount).IsEqualTo(1);
            await Assert.That(resultByRole.Data.Items[0].Id).IsEqualTo(user3.Id);
        }

        [Test]
        public async Task GetUserListAsync_WhenSortingAndPaginationApplied_ShouldReturnCorrectPageAndOrder()
        {
            // Arrange
            var userA = new User { Id = Guid.NewGuid(), FirstName = "Adam", LastName = "A", CreatedAt = DateTime.UtcNow.AddDays(-3) };
            var userB = new User { Id = Guid.NewGuid(), FirstName = "Bartosz", LastName = "B", CreatedAt = DateTime.UtcNow.AddDays(-2) };
            var userC = new User { Id = Guid.NewGuid(), FirstName = "Cezary", LastName = "C", CreatedAt = DateTime.UtcNow.AddDays(-1) };

            await _contextMock.Users.AddRangeAsync(userA, userB, userC);
            await _contextMock.SaveChangesAsync();

            var command = new UserListCommand
            {
                PageNumber = 2,
                PageSize = 2,
                SortBy = "FirstName",
                SortDescending = true
            };

            // Act
            var result = await _userServicesMock.GetUserListAsnyc(command);

            // Assert 
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(result.Data!.TotalCount).IsEqualTo(3);
            await Assert.That(result.Data.TotalPages).IsEqualTo(2);
            await Assert.That(result.Data.PageNumber).IsEqualTo(2);
            await Assert.That(result.Data.Items.Count).IsEqualTo(1);
            await Assert.That(result.Data.Items[0].FirstName).IsEqualTo("Adam");
        }

        [Test]
        public async Task GetUserListAsync_WhenNoUsersMatchCriteria_ShouldReturnEmptyPagedResult()
        {
            // Arrange
            await _contextMock.Users.AddAsync(new User { Id = Guid.NewGuid(), FirstName = "Jan", LastName = "Kowalski" });
            await _contextMock.SaveChangesAsync();

            var command = new UserListCommand
            {
                SearchTerm = "NieistniejacyUzytkownik123",
                PageNumber = 1,
                PageSize = 10
            };

            // Act
            var result = await _userServicesMock.GetUserListAsnyc(command);

            // Assert
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(result.Message).IsEqualTo("No UserList found.");
            await Assert.That(result.Data).IsNotNull();
            await Assert.That(result.Data!.Items.Count).IsEqualTo(0);
            await Assert.That(result.Data.TotalCount).IsEqualTo(0);
            await Assert.That(result.Data.TotalPages).IsEqualTo(0);
        }

        [Test]
        public async Task GetUserListAsync_WhenPageNumberExceedsTotalPages_ShouldClampToLastPage()
        {
            // Arrange
            await _contextMock.Users.AddRangeAsync(
                new User { Id = Guid.NewGuid(), FirstName = "User1", LastName = "Test" },
                new User { Id = Guid.NewGuid(), FirstName = "User2", LastName = "Test" }
            );
            await _contextMock.SaveChangesAsync();

            var command = new UserListCommand
            {
                PageNumber = 99,
                PageSize = 2
            };

            // Act
            var result = await _userServicesMock.GetUserListAsnyc(command);

            // Assert
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(result.Data!.PageNumber).IsEqualTo(1);
            await Assert.That(result.Data.TotalPages).IsEqualTo(1);
            await Assert.That(result.Data.Items.Count).IsEqualTo(2);
        }
        #endregion

        #region GetUserDetailAsync
        [Test]
        public async Task GetUserDetailAsync_WhenUserExistsWithRole_ShouldReturnUserDetails()
        {
            // Arrange
            var roleId = Guid.NewGuid();
            var userId = Guid.NewGuid();

            var role = new Microsoft.AspNetCore.Identity.IdentityRole<Guid>
            {
                Id = roleId,
                Name = "Moderator",
                NormalizedName = "MODERATOR"
            };

            var user = new User
            {
                Id = userId,
                FirstName = "Jan",
                LastName = "Kowalski",
                Email = "jan.kowalski@test.pl",
                IsPublic = true,
                IsBanned = false
            };

            await _contextMock.Roles.AddAsync(role);
            await _contextMock.Users.AddAsync(user);
            await _contextMock.UserRoles.AddAsync(new Microsoft.AspNetCore.Identity.IdentityUserRole<Guid>
            {
                UserId = userId,
                RoleId = roleId
            });
            await _contextMock.SaveChangesAsync();

            // Act
            var result = await _userServicesMock.GetUserDetailAsync(userId);

            // Assert
            await Assert.That(result).IsNotNull();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(result.Message).IsEqualTo("User details retrieved successfully.");
            await Assert.That(result.Data).IsNotNull();
            await Assert.That(result.Data!.Id).IsEqualTo(userId);
            await Assert.That(result.Data.FirstName).IsEqualTo("Jan");
            await Assert.That(result.Data.LastName).IsEqualTo("Kowalski");
            await Assert.That(result.Data.Email).IsEqualTo("jan.kowalski@test.pl");
            await Assert.That(result.Data.Role).IsEqualTo("Moderator");
            await Assert.That(result.Data.IsPublic).IsTrue();
        }

        [Test]
        public async Task GetUserDetailAsync_WhenUserHasNoRole_ShouldReturnEmptyRoleString()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var user = new User
            {
                Id = userId,
                FirstName = "Anna",
                LastName = "Nowak",
                Email = "anna.nowak@test.pl",
                IsPublic = false,
                IsBanned = true
            };

            await _contextMock.Users.AddAsync(user);
            await _contextMock.SaveChangesAsync();

            // Act
            var result = await _userServicesMock.GetUserDetailAsync(userId);

            // Assert
            await Assert.That(result).IsNotNull();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(result.Data).IsNotNull();
            await Assert.That(result.Data!.Role).IsEqualTo(string.Empty);
            await Assert.That(result.Data.IsPublic).IsFalse();
        }

        [Test]
        public async Task GetUserDetailAsync_WhenUserDoesNotExist_ShouldReturnNotFoundFailure()
        {
            // Arrange
            var nonExistentUserId = Guid.NewGuid();

            // Act
            var result = await _userServicesMock.GetUserDetailAsync(nonExistentUserId);

            // Assert
            await Assert.That(result).IsNotNull();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status404NotFound);
            await Assert.That(result.ErrorCode).IsEqualTo(Domain.Const.ErrorCodes.NotFound);
            await Assert.That(result.Message).IsEqualTo($"User with ID '{nonExistentUserId}' was not found.");
            await Assert.That(result.Data).IsNull();
        }

        [Test]
        [Arguments(null)]
        [Arguments("")]
        [Arguments("   ")]
        public async Task GetUserDetailAsync_WhenUserEmailIsNullOrWhitespace_ShouldThrowInternalServerException(string? invalidEmail)
        {
            // Arrange
            var userId = Guid.NewGuid();
            var corruptedUser = new User
            {
                Id = userId,
                FirstName = "Piotr",
                LastName = "Uszkodzony",
                Email = invalidEmail
            };

            await _contextMock.Users.AddAsync(corruptedUser);
            await _contextMock.SaveChangesAsync();

            // Act & Assert
            await Assert.That(async () => await _userServicesMock.GetUserDetailAsync(userId))
                .Throws<InternalServerException>()
                .WithMessage("User email is required.");
        }
        #endregion

        #region GetUserBanDetailAsync
        [Test]
        public async Task GetUserBanDetailAsync_WhenUserIsBanned_ShouldReturnBanDetails()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var bannedUntil = new DateTime(2027, 1, 15, 12, 0, 0, DateTimeKind.Utc);
            var bannedReason = "Violation of community guidelines.";

            var user = new User
            {
                Id = userId,
                FirstName = "Jan",
                LastName = "Kowalski",
                IsBanned = true,
                BannedUntil = bannedUntil,
                BannedReason = bannedReason
            };

            await _contextMock.Users.AddAsync(user);
            await _contextMock.SaveChangesAsync();

            // Act
            var result = await _userServicesMock.GetUserBanDetailAsync(userId);

            // Assert
            await Assert.That(result).IsNotNull();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(result.Message).IsEqualTo("User ban details retrieved successfully.");
            await Assert.That(result.Data).IsNotNull();
            await Assert.That(result.Data!.IsBanned).IsTrue();
            await Assert.That(result.Data.BannedUntil).IsEqualTo(bannedUntil);
            await Assert.That(result.Data.BannedReason).IsEqualTo(bannedReason);
        }

        [Test]
        public async Task GetUserBanDetailAsync_WhenUserIsNotBanned_ShouldReturnDefaultUnbannedDetails()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var user = new User
            {
                Id = userId,
                FirstName = "Anna",
                LastName = "Nowak",
                IsBanned = false,
                BannedUntil = null,
                BannedReason = null
            };

            await _contextMock.Users.AddAsync(user);
            await _contextMock.SaveChangesAsync();

            // Act
            var result = await _userServicesMock.GetUserBanDetailAsync(userId);

            // Assert
            await Assert.That(result).IsNotNull();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(result.Message).IsEqualTo("User ban details retrieved successfully.");
            await Assert.That(result.Data).IsNotNull();
            await Assert.That(result.Data!.IsBanned).IsFalse();
            await Assert.That(result.Data.BannedUntil).IsNull();
            await Assert.That(result.Data.BannedReason).IsNull();
        }

        [Test]
        public async Task GetUserBanDetailAsync_WhenUserDoesNotExist_ShouldReturnNotFoundFailure()
        {
            // Arrange
            var nonExistentUserId = Guid.NewGuid();

            // Act
            var result = await _userServicesMock.GetUserBanDetailAsync(nonExistentUserId);

            // Assert
            await Assert.That(result).IsNotNull();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status404NotFound);
            await Assert.That(result.ErrorCode).IsEqualTo(Domain.Const.ErrorCodes.NotFound);
            await Assert.That(result.Message).IsEqualTo($"User with ID '{nonExistentUserId}' was not found.");
            await Assert.That(result.Data).IsNull();
        }
        #endregion

        #region GetUserReportListAsync
        [Test]
        public async Task GetUserReportListAsync_WhenUserHasNoReports_ShouldReturnEmptyPagedResult()
        {
            // Arrange
            var user = await AddReportTestUserAsync("Anna", "Nowak");
            var command = new SimpleListCommand { PageNumber = 1, PageSize = 10 };

            // Act
            var result = await _userServicesMock.GetUserReportListAsync(user.Id, command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(result.Data).IsNotNull();
            await Assert.That(result.Data!.Items).IsEmpty();
            await Assert.That(result.Data.TotalCount).IsEqualTo(0);
        }

        [Test]
        public async Task GetUserReportListAsync_WhenUserDoesNotExist_ShouldReturnEmptyPagedResult()
        {
            // Arrange
            var nonExistentUserId = Guid.NewGuid();
            var command = new SimpleListCommand { PageNumber = 1, PageSize = 10 };

            // Act
            var result = await _userServicesMock.GetUserReportListAsync(nonExistentUserId, command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(result.Data!.Items).IsEmpty();
            await Assert.That(result.Data.TotalCount).IsEqualTo(0);
        }

        [Test]
        public async Task GetUserReportListAsync_WhenUserHasReports_ShouldReturnOnlyReportsForThatUserAndMapAllFields()
        {
            // Arrange
            var baseDate = new DateTime(2025, 1, 10, 12, 0, 0, DateTimeKind.Utc);
            var resolvedAt = baseDate.AddDays(2);

            var targetUser = await AddReportTestUserAsync("Anna", "Nowak");
            var otherUser = await AddReportTestUserAsync("Piotr", "Zielinski");
            var reporter = await AddReportTestUserAsync("Adam", "Reporter");

            var report = await AddUserReportAsync(
                reported: targetUser,
                reporter: reporter,
                reason: "Spamming links in comments",
                status: Domain.Enum.ReportStatusEnum.Approved,
                createdAt: baseDate,
                resolvedAt: resolvedAt);

            // Zgłoszenie dla innego użytkownika oraz zgłoszenie złożone przez targetUser (nie przeciwko niemu)
            await AddUserReportAsync(reported: otherUser, reporter: reporter, reason: "Other user report", createdAt: baseDate);
            await AddUserReportAsync(reported: otherUser, reporter: targetUser, reason: "Report submitted by target user", createdAt: baseDate);

            var command = new SimpleListCommand { PageNumber = 1, PageSize = 10 };

            // Act
            var result = await _userServicesMock.GetUserReportListAsync(targetUser.Id, command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(result.Data!.TotalCount).IsEqualTo(1);

            var item = result.Data.Items.Single();
            await Assert.That(item.Id).IsEqualTo(report.Id);
            await Assert.That(item.Reason).IsEqualTo("Spamming links in comments");
            await Assert.That(item.Status).IsEqualTo(Domain.Enum.ReportStatusEnum.Approved);
            await Assert.That(item.CreatedAt).IsEqualTo(baseDate);
            await Assert.That(item.ResolvedAt).IsEqualTo(resolvedAt);
        }

        [Test]
        public async Task GetUserReportListAsync_WhenSortByIsNull_ShouldDefaultToCreatedAtDescending()
        {
            // Arrange
            var baseDate = new DateTime(2025, 1, 10, 12, 0, 0, DateTimeKind.Utc);
            var targetUser = await AddReportTestUserAsync("Anna", "Nowak");
            var reporter = await AddReportTestUserAsync("Adam", "Reporter");

            var oldest = await AddUserReportAsync(targetUser, reporter, createdAt: baseDate);
            var middle = await AddUserReportAsync(targetUser, reporter, createdAt: baseDate.AddDays(1));
            var newest = await AddUserReportAsync(targetUser, reporter, createdAt: baseDate.AddDays(2));

            var command = new SimpleListCommand { SortBy = null, PageNumber = 1, PageSize = 10 };

            // Act
            var result = await _userServicesMock.GetUserReportListAsync(targetUser.Id, command);

            // Assert
            var ids = result.Data!.Items.Select(i => i.Id).ToList();
            await Assert.That(ids.SequenceEqual(new[] { newest.Id, middle.Id, oldest.Id })).IsTrue();
        }

        [Test]
        public async Task GetUserReportListAsync_WhenSortByCreatedAtAscending_ShouldReturnOldestFirst()
        {
            // Arrange
            var baseDate = new DateTime(2025, 1, 10, 12, 0, 0, DateTimeKind.Utc);
            var targetUser = await AddReportTestUserAsync("Anna", "Nowak");
            var reporter = await AddReportTestUserAsync("Adam", "Reporter");

            var newest = await AddUserReportAsync(targetUser, reporter, createdAt: baseDate.AddDays(2));
            var oldest = await AddUserReportAsync(targetUser, reporter, createdAt: baseDate);
            var middle = await AddUserReportAsync(targetUser, reporter, createdAt: baseDate.AddDays(1));

            var command = new SimpleListCommand
            {
                SortBy = "createdAt",
                SortDescending = false,
                PageNumber = 1,
                PageSize = 10
            };

            // Act
            var result = await _userServicesMock.GetUserReportListAsync(targetUser.Id, command);

            // Assert
            var ids = result.Data!.Items.Select(i => i.Id).ToList();
            await Assert.That(ids.SequenceEqual(new[] { oldest.Id, middle.Id, newest.Id })).IsTrue();
        }

        [Test]
        public async Task GetUserReportListAsync_WhenSortByStatus_ShouldSortByStatusInRequestedDirection()
        {
            // Arrange
            var baseDate = new DateTime(2025, 1, 10, 12, 0, 0, DateTimeKind.Utc);
            var targetUser = await AddReportTestUserAsync("Anna", "Nowak");
            var reporter = await AddReportTestUserAsync("Adam", "Reporter");

            await AddUserReportAsync(
                targetUser, reporter, status: Domain.Enum.ReportStatusEnum.Rejected,
                createdAt: baseDate.AddDays(1), resolvedAt: baseDate.AddDays(2));
            await AddUserReportAsync(
                targetUser, reporter, status: Domain.Enum.ReportStatusEnum.Pending,
                createdAt: baseDate.AddDays(3));
            await AddUserReportAsync(
                targetUser, reporter, status: Domain.Enum.ReportStatusEnum.Approved,
                createdAt: baseDate.AddDays(2), resolvedAt: baseDate.AddDays(3));

            var expectedAscending = await _contextMock.UserReports
                .AsNoTracking()
                .Where(r => r.ReportedUserId == targetUser.Id)
                .OrderBy(r => r.Status)
                .ThenBy(r => r.Id)
                .Select(r => r.Id)
                .ToListAsync();

            var expectedDescending = await _contextMock.UserReports
                .AsNoTracking()
                .Where(r => r.ReportedUserId == targetUser.Id)
                .OrderByDescending(r => r.Status)
                .ThenBy(r => r.Id)
                .Select(r => r.Id)
                .ToListAsync();

            var ascCommand = new SimpleListCommand
            {
                SortBy = "status",
                SortDescending = false,
                PageNumber = 1,
                PageSize = 10
            };
            var descCommand = new SimpleListCommand
            {
                SortBy = "status",
                SortDescending = true,
                PageNumber = 1,
                PageSize = 10
            };

            // Act
            var ascending = await _userServicesMock.GetUserReportListAsync(targetUser.Id, ascCommand);
            var descending = await _userServicesMock.GetUserReportListAsync(targetUser.Id, descCommand);

            // Assert
            await Assert.That(ascending.Data!.Items.Select(i => i.Id)
                .SequenceEqual(expectedAscending)).IsTrue();
            await Assert.That(descending.Data!.Items.Select(i => i.Id)
                .SequenceEqual(expectedDescending)).IsTrue();
        }

        [Test]
        public async Task GetUserReportListAsync_WhenSortByResolvedAt_ShouldSortByResolvedAtInRequestedDirection()
        {
            // Arrange
            var baseDate = new DateTime(2025, 1, 10, 12, 0, 0, DateTimeKind.Utc);
            var targetUser = await AddReportTestUserAsync("Anna", "Nowak");
            var reporter = await AddReportTestUserAsync("Adam", "Reporter");

            var resolvedFirst = await AddUserReportAsync(
                targetUser, reporter, status: Domain.Enum.ReportStatusEnum.Approved,
                createdAt: baseDate, resolvedAt: baseDate.AddDays(1));
            var resolvedSecond = await AddUserReportAsync(
                targetUser, reporter, status: Domain.Enum.ReportStatusEnum.Rejected,
                createdAt: baseDate.AddDays(1), resolvedAt: baseDate.AddDays(5));

            var ascCommand = new SimpleListCommand
            {
                SortBy = "resolvedAt",
                SortDescending = false,
                PageNumber = 1,
                PageSize = 10
            };
            var descCommand = new SimpleListCommand
            {
                SortBy = "resolvedAt",
                SortDescending = true,
                PageNumber = 1,
                PageSize = 10
            };

            // Act
            var ascending = await _userServicesMock.GetUserReportListAsync(targetUser.Id, ascCommand);
            var descending = await _userServicesMock.GetUserReportListAsync(targetUser.Id, descCommand);

            // Assert
            await Assert.That(ascending.Data!.Items.Select(i => i.Id)
                .SequenceEqual(new[] { resolvedFirst.Id, resolvedSecond.Id })).IsTrue();
            await Assert.That(descending.Data!.Items.Select(i => i.Id)
                .SequenceEqual(new[] { resolvedSecond.Id, resolvedFirst.Id })).IsTrue();
        }

        [Test]
        public async Task GetUserReportListAsync_WhenPaginationApplied_ShouldReturnRequestedPageOnly()
        {
            // Arrange
            var baseDate = new DateTime(2025, 1, 10, 12, 0, 0, DateTimeKind.Utc);
            var targetUser = await AddReportTestUserAsync("Anna", "Nowak");
            var reporter = await AddReportTestUserAsync("Adam", "Reporter");

            for (var i = 0; i < 5; i++)
            {
                await AddUserReportAsync(
                    targetUser,
                    reporter,
                    reason: $"Report {i}",
                    createdAt: baseDate.AddDays(i));
            }

            var command = new SimpleListCommand
            {
                SortBy = "createdAt",
                SortDescending = false,
                PageNumber = 2,
                PageSize = 2
            };

            // Act
            var result = await _userServicesMock.GetUserReportListAsync(targetUser.Id, command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.Data!.TotalCount).IsEqualTo(5);
            await Assert.That(result.Data.TotalPages).IsEqualTo(3);
            await Assert.That(result.Data.PageNumber).IsEqualTo(2);
            await Assert.That(result.Data.Items.Count).IsEqualTo(2);
            await Assert.That(result.Data.Items[0].Reason).IsEqualTo("Report 2");
            await Assert.That(result.Data.Items[1].Reason).IsEqualTo("Report 3");
        }

        private async Task<User> AddReportTestUserAsync(string firstName, string lastName)
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
                FirstName = firstName,
                LastName = lastName
            };

            await _contextMock.Users.AddAsync(user);
            await _contextMock.SaveChangesAsync();
            return user;
        }

        private async Task<UserReport> AddUserReportAsync(
            User reported,
            User reporter,
            string reason = "Spam content",
            Domain.Enum.ReportStatusEnum status = Domain.Enum.ReportStatusEnum.Pending,
            DateTime? createdAt = null,
            DateTime? resolvedAt = null)
        {
            var report = new UserReport
            {
                Id = Guid.NewGuid(),
                ReportedUserId = reported.Id,
                ReporterUserId = reporter.Id,
                Reason = reason,
                Status = status,
                ResolvedAt = resolvedAt
            };

            await _contextMock.UserReports.AddAsync(report);
            await _contextMock.SaveChangesAsync();

            if (createdAt.HasValue)
            {
                await _contextMock.UserReports
                    .Where(r => r.Id == report.Id)
                    .ExecuteUpdateAsync(s => s.SetProperty(r => r.CreatedAt, createdAt.Value));

                await _contextMock.Entry(report).ReloadAsync();
            }

            return report;
        }
        #endregion
    }
}
