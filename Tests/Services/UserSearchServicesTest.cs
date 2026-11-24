using Domain.Models;
using Infrastructure;
using Infrastructure.Interceptors;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using Services.Accessors.Interfaces;
using Services.Command.List;
using Services.Interfaces;
using Services.Response.User;
using Services.Services;
using Testcontainers.PostgreSql;
using Tests.Fakes;
using TUnit.Assertions.Enums;

namespace Tests.Services
{
    public class UserSearchServicesTest
    {
        #region Configuration and Setup
        protected AppDbContext _context = null!;
        private static PostgreSqlContainer _dbContainer = null!;
        private static string _connectionString = null!;

        protected IUserServices _sut = null!;
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

            _context = new AppDbContext(dbOptions);

            var createScript = _context.Database.GenerateCreateScript();
            await _context.Database.ExecuteSqlRawAsync(createScript);

            _ctMock = new FakeCancellationTokenAccessor();

            _sut = new UserServices(
                _context,
                new LoggerFactory().CreateLogger<UserServices>(),
                _ctMock);
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
        #endregion

        #region Search matching
        [Test]
        public async Task SearchUsersAsync_WhenQueryMatchesPartialFirstName_ShouldReturnMatch()
        {
            // Arrange
            var jane = await SeedUserAsync("Jane", "Doe");
            await SeedUserAsync("John", "Smith");

            // Act
            var result = await _sut.SearchUsersAsync(new BasicListCommand { SearchTerm = "jan" });

            // Assert
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(result.Data!.Items.Count).IsEqualTo(1);
            await Assert.That(result.Data.Items[0].UserId).IsEqualTo(jane.Id);
        }

        [Test]
        public async Task SearchUsersAsync_WhenQueryMatchesPartialLastName_ShouldReturnMatch()
        {
            // Arrange
            var jane = await SeedUserAsync("Jane", "Doe");
            await SeedUserAsync("John", "Smith");

            // Act
            var result = await _sut.SearchUsersAsync(new BasicListCommand { SearchTerm = "oe" });

            // Assert
            await Assert.That(result.Data!.Items.Count).IsEqualTo(1);
            await Assert.That(result.Data.Items[0].UserId).IsEqualTo(jane.Id);
        }

        [Test]
        [Arguments("JANE")]
        [Arguments("jAnE")]
        public async Task SearchUsersAsync_WhenQueryDiffersInCase_ShouldReturnMatch(string query)
        {
            // Arrange
            var jane = await SeedUserAsync("Jane", "Doe");

            // Act
            var result = await _sut.SearchUsersAsync(new BasicListCommand { SearchTerm = query });

            // Assert
            await Assert.That(result.Data!.Items.Count).IsEqualTo(1);
            await Assert.That(result.Data.Items[0].UserId).IsEqualTo(jane.Id);
        }

        [Test]
        public async Task SearchUsersAsync_WhenQueryIsFullName_ShouldMatchEveryTerm()
        {
            // Arrange
            var janeDoe = await SeedUserAsync("Jane", "Doe");
            await SeedUserAsync("Jane", "Smith");
            await SeedUserAsync("John", "Doe");

            // Act
            var result = await _sut.SearchUsersAsync(new BasicListCommand { SearchTerm = "Jane Doe" });

            // Assert
            await Assert.That(result.Data!.Items.Count).IsEqualTo(1);
            await Assert.That(result.Data.Items[0].UserId).IsEqualTo(janeDoe.Id);
        }

        [Test]
        [Arguments(null)]
        [Arguments("")]
        [Arguments("   ")]
        public async Task SearchUsersAsync_WhenQueryIsEmptyOrWhitespace_ShouldReturnAllPublicActiveUsers(string? query)
        {
            // Arrange
            await SeedUserAsync("Jane", "Doe");
            await SeedUserAsync("John", "Smith");
            await SeedUserAsync("Private", "Person", isPublic: false);
            await SeedUserAsync("Deleted", "Person", isDeleted: true);

            // Act
            var result = await _sut.SearchUsersAsync(new BasicListCommand { SearchTerm = query });

            // Assert
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(result.Data!.Items.Count).IsEqualTo(2);
            await Assert.That(result.Data.TotalCount).IsEqualTo(2);
        }

        [Test]
        public async Task SearchUsersAsync_WhenQueryOmitsDiacritics_ShouldMatchAccentedName()
        {
            // Arrange
            var user = await SeedUserAsync("Łukasz", "Żółw");

            // Act
            var result = await _sut.SearchUsersAsync(new BasicListCommand { SearchTerm = "lukasz zolw" });

            // Assert
            await Assert.That(result.Data!.Items.Count).IsEqualTo(1);
            await Assert.That(result.Data.Items[0].UserId).IsEqualTo(user.Id);
        }
        #endregion

        #region Privacy and status filtering
        [Test]
        public async Task SearchUsersAsync_WhenMatchIsPrivate_ShouldNotReturnIt()
        {
            // Arrange
            await SeedUserAsync("Jane", "Doe", isPublic: false);

            // Act
            var result = await _sut.SearchUsersAsync(new BasicListCommand { SearchTerm = "Jane Doe" });

            // Assert
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(result.Data!.Items).IsEmpty();
            await Assert.That(result.Data.TotalCount).IsEqualTo(0);
        }

        [Test]
        public async Task SearchUsersAsync_WhenMatchIsSoftDeleted_ShouldNotReturnIt()
        {
            // Arrange
            await SeedUserAsync("Jane", "Doe", isDeleted: true);

            // Act
            var result = await _sut.SearchUsersAsync(new BasicListCommand { SearchTerm = "Jane Doe" });

            // Assert
            await Assert.That(result.Data!.Items).IsEmpty();
            await Assert.That(result.Data.TotalCount).IsEqualTo(0);
        }

        [Test]
        public async Task SearchUsersAsync_WhenMatchIsPermanentlyBanned_ShouldNotReturnIt()
        {
            // Arrange
            await SeedUserAsync("Jane", "Doe", isBanned: true);

            // Act
            var result = await _sut.SearchUsersAsync(new BasicListCommand { SearchTerm = "Jane Doe" });

            // Assert
            await Assert.That(result.Data!.Items).IsEmpty();
            await Assert.That(result.Data.TotalCount).IsEqualTo(0);
        }

        [Test]
        public async Task SearchUsersAsync_WhenMatchIsBannedUntilFutureDate_ShouldNotReturnIt()
        {
            // Arrange
            await SeedUserAsync("Jane", "Doe", isBanned: true, bannedUntil: DateTime.UtcNow.AddDays(7));

            // Act
            var result = await _sut.SearchUsersAsync(new BasicListCommand { SearchTerm = "Jane Doe" });

            // Assert
            await Assert.That(result.Data!.Items).IsEmpty();
            await Assert.That(result.Data.TotalCount).IsEqualTo(0);
        }

        [Test]
        public async Task SearchUsersAsync_WhenMatchHasExpiredBan_ShouldReturnIt()
        {
            // Arrange
            var jane = await SeedUserAsync("Jane", "Doe", isBanned: true, bannedUntil: DateTime.UtcNow.AddDays(-1));

            // Act
            var result = await _sut.SearchUsersAsync(new BasicListCommand { SearchTerm = "Jane Doe" });

            // Assert
            await Assert.That(result.Data!.Items.Count).IsEqualTo(1);
            await Assert.That(result.Data.Items[0].UserId).IsEqualTo(jane.Id);
        }
        #endregion

        #region Sorting
        [Test]
        public async Task SearchUsersAsync_WhenSortingByNameAscending_ShouldOrderByFirstNameThenLastName()
        {
            // Arrange
            await SeedUserAsync("Charlie", "Zulu");
            await SeedUserAsync("Alpha", "Yankee");
            await SeedUserAsync("Bravo", "Xray");

            // Act
            var result = await _sut.SearchUsersAsync(new BasicListCommand { SortBy = "firstname", SortDescending = false });

            // Assert
            var names = result.Data!.Items.Select(u => u.FirstName).ToList();
            await Assert.That(names).IsEquivalentTo(new[] { "Alpha", "Bravo", "Charlie" }, CollectionOrdering.Matching);
        }

        [Test]
        public async Task SearchUsersAsync_WhenSortingByNameDescending_ShouldReverseAlphabeticalOrder()
        {
            // Arrange
            await SeedUserAsync("Charlie", "Zulu");
            await SeedUserAsync("Alpha", "Yankee");
            await SeedUserAsync("Bravo", "Xray");

            // Act
            var result = await _sut.SearchUsersAsync(new BasicListCommand { SortBy = "firstname", SortDescending = true });

            // Assert
            var names = result.Data!.Items.Select(u => u.FirstName).ToList();
            await Assert.That(names).IsEquivalentTo(new[] { "Charlie", "Bravo", "Alpha" }, CollectionOrdering.Matching);
        }

        [Test]
        public async Task SearchUsersAsync_WhenSortIsNotProvided_ShouldUseNameAscending()
        {
            // Arrange
            await SeedUserAsync("Charlie", "Zulu");
            await SeedUserAsync("Alpha", "Yankee");

            // Act
            var result = await _sut.SearchUsersAsync(new BasicListCommand());

            // Assert
            var names = result.Data!.Items.Select(u => u.FirstName).ToList();
            await Assert.That(names).IsEquivalentTo(new[] { "Alpha", "Charlie" }, CollectionOrdering.Matching);
        }

        [Test]
        [Arguments(true)]
        [Arguments(false)]
        public async Task SearchUsersAsync_WhenSortingByRegistrationDate_ShouldOrderByCreatedAt(bool sortDescending)
        {
            // Arrange
            var now = DateTime.UtcNow;
            var oldest = await SeedUserAsync("Charlie", "Zulu", createdAt: now.AddDays(-3));
            var middle = await SeedUserAsync("Alpha", "Yankee", createdAt: now.AddDays(-2));
            var newest = await SeedUserAsync("Bravo", "Xray", createdAt: now.AddDays(-1));

            var expected = sortDescending
                ? new[] { newest.Id, middle.Id, oldest.Id }
                : new[] { oldest.Id, middle.Id, newest.Id };

            // Act
            var result = await _sut.SearchUsersAsync(new BasicListCommand { SortBy = "createdat", SortDescending = sortDescending });

            // Assert
            var ids = result.Data!.Items.Select(u => u.UserId).ToList();
            await Assert.That(ids).IsEquivalentTo(expected, CollectionOrdering.Matching);
        }
        #endregion

        #region Pagination and metadata
        [Test]
        public async Task SearchUsersAsync_WhenPaging_ShouldReturnCorrectSliceAndMetadata()
        {
            // Arrange
            for (int i = 1; i <= 5; i++)
            {
                await SeedUserAsync($"User{i:00}", "Public");
            }

            await SeedUserAsync("Private", "User", isPublic: false);
            await SeedUserAsync("Deleted", "User", isDeleted: true);

            // Act
            var firstPage = await _sut.SearchUsersAsync(new BasicListCommand { PageNumber = 1, PageSize = 2 });
            var lastPage = await _sut.SearchUsersAsync(new BasicListCommand { PageNumber = 3, PageSize = 2 });

            // Assert
            await Assert.That(firstPage.Data!.Items.Count).IsEqualTo(2);
            await Assert.That(firstPage.Data.PageNumber).IsEqualTo(1);
            await Assert.That(firstPage.Data.PageSize).IsEqualTo(2);
            await Assert.That(firstPage.Data.TotalCount).IsEqualTo(5);
            await Assert.That(firstPage.Data.TotalPages).IsEqualTo(3);
            await Assert.That(firstPage.Data.HasNextPage).IsTrue();

            await Assert.That(lastPage.Data!.Items.Count).IsEqualTo(1);
            await Assert.That(lastPage.Data.PageNumber).IsEqualTo(3);
            await Assert.That(lastPage.Data.HasPreviousPage).IsTrue();
            await Assert.That(lastPage.Data.HasNextPage).IsFalse();
        }

        [Test]
        public async Task SearchUsersAsync_WhenPageExceedsTotalPages_ShouldClampToLastPage()
        {
            // Arrange
            for (int i = 1; i <= 3; i++)
            {
                await SeedUserAsync($"User{i:00}", "Public");
            }

            // Act
            var result = await _sut.SearchUsersAsync(new BasicListCommand { PageNumber = 99, PageSize = 2 });

            // Assert
            await Assert.That(result.Data!.PageNumber).IsEqualTo(2);
            await Assert.That(result.Data.Items.Count).IsEqualTo(1);
            await Assert.That(result.Data.HasNextPage).IsFalse();
        }

        [Test]
        public async Task SearchUsersAsync_WhenPageSizeIsNotProvided_ShouldUseDefaultPageSize()
        {
            // Arrange
            for (int i = 1; i <= 25; i++)
            {
                await SeedUserAsync($"User{i:00}", "Public");
            }

            // Act
            var result = await _sut.SearchUsersAsync(new BasicListCommand());

            // Assert
            await Assert.That(result.Data!.PageSize).IsEqualTo(10);
            await Assert.That(result.Data.Items.Count).IsEqualTo(10);
            await Assert.That(result.Data.TotalCount).IsEqualTo(25);
        }

        [Test]
        public async Task SearchUsersAsync_WhenPageSizeExceedsMaximum_ShouldClampToHundred()
        {
            // Arrange
            await SeedUserAsync("Jane", "Doe");

            // Act
            var result = await _sut.SearchUsersAsync(new BasicListCommand { PageSize = 500 });

            // Assert
            await Assert.That(result.Data!.PageSize).IsEqualTo(100);
        }
        #endregion

        #region Empty and response shape
        [Test]
        public async Task SearchUsersAsync_WhenNothingMatches_ShouldReturnEmptySuccessResult()
        {
            // Arrange
            await SeedUserAsync("Jane", "Doe");

            // Act
            var result = await _sut.SearchUsersAsync(new BasicListCommand { SearchTerm = "zzzzzz" });

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(result.Data!.Items).IsEmpty();
            await Assert.That(result.Data.TotalCount).IsEqualTo(0);
            await Assert.That(result.Data.TotalPages).IsEqualTo(0);
            await Assert.That(result.Data.HasNextPage).IsFalse();
        }

        [Test]
        public async Task UserSummaryResponse_ShouldExposeOnlySummaryFields()
        {
            // Act
            var properties = typeof(UserSummaryResponse)
                .GetProperties()
                .Select(p => p.Name)
                .OrderBy(name => name)
                .ToArray();

            // Assert
            await Assert.That(properties.Length).IsEqualTo(3);
            await Assert.That(properties).Contains(nameof(UserSummaryResponse.UserId));
            await Assert.That(properties).Contains(nameof(UserSummaryResponse.FirstName));
            await Assert.That(properties).Contains(nameof(UserSummaryResponse.LastName));
        }
        #endregion

        private async Task<User> SeedUserAsync(
            string firstName,
            string lastName,
            bool isPublic = true,
            bool isBanned = false,
            DateTime? bannedUntil = null,
            bool isDeleted = false,
            DateTime? createdAt = null)
        {
            var user = new User
            {
                Id = Guid.NewGuid(),
                FirstName = firstName,
                LastName = lastName,
                UserName = $"{firstName}.{lastName}.{Guid.NewGuid():N}@example.com",
                IsPublic = isPublic,
                IsBanned = isBanned,
                BannedUntil = bannedUntil,
                IsDeleted = isDeleted,
                DeletedAt = isDeleted ? DateTime.UtcNow : null,
                CreatedAt = createdAt ?? DateTime.UtcNow
            };

            await _context.Users.AddAsync(user);
            await _context.SaveChangesAsync();

            return user;
        }
    }
}
