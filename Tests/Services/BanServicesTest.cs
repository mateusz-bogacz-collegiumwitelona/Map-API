using Domain.Const;
using Domain.Enum;
using Domain.Models;
using Infrastructure;
using Infrastructure.Interceptors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using Services.Command.Ban;
using Services.Command.List;
using Services.Interfaces;
using Services.Interfaces.Auth;
using Services.Options;
using Services.Services;
using Services.Services.Auth;
using Testcontainers.PostgreSql;
using Tests.Fakes;

namespace Tests.Services
{
    public class BanServicesTest
    {
        #region Configuration and Setup
        private AppDbContext _context = null!;
        private static PostgreSqlContainer _dbContainer = null!;
        private static string _connectionString = null!;

        private IBanServices _sut = null!;
        private string _currentSchema = null!;

        private UserManager<User> _userManager = null!;

        private FakeEmailService _emailService = null!;

        private ITokenService _tokenService = null!;

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

            var userStore = new UserStore<User, IdentityRole<Guid>, AppDbContext, Guid>(_context);
            _userManager = new UserManager<User>(
                userStore,
                Options.Create(new IdentityOptions()),
                new PasswordHasher<User>(),
                Array.Empty<IUserValidator<User>>(),
                Array.Empty<IPasswordValidator<User>>(),
                new UpperInvariantLookupNormalizer(),
                new IdentityErrorDescriber(),
                null!,
                new LoggerFactory().CreateLogger<UserManager<User>>());

            _emailService = new FakeEmailService();
            _tokenService = new TokenService(_context, Options.Create(new JwtOptions
            {
                Issuer = "TestIssuer",
                Audience = "TestAudience",
                AccessTokenExpirationMinutes = 60,
                RefreshTokenExpirationDays = 7,
                SecretKey = "ThisIsASecretKeyForTestingPurposesOnly!"
            }));

            _sut = new BanServices(
                _context,
                new LoggerFactory().CreateLogger<BanServices>(),
                new FakeCancellationTokenAccessor(),
                _userManager,
                _emailService,
                _tokenService);
        }

        [After(Test)]
        public async Task CleanupAsync()
        {
            _userManager.Dispose();
            await _context.DisposeAsync();

            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"DROP SCHEMA IF EXISTS {_currentSchema} CASCADE;";
            await cmd.ExecuteNonQueryAsync();
        }

        private async Task<User> AddUserAsync(
            string firstName = "Jan",
            string lastName = "Kowalski",
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
                LockoutEnabled = true,
                FirstName = firstName,
                LastName = lastName,
                IsBanned = isBanned,
                BannedUntil = bannedUntil,
                BannedReason = bannedReason
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return user;
        }

        private static ResolveReportCommand CreateResolveCommand(
            Guid reportId,
            bool isBanned = false,
            TimeSpan? duration = null,
            string? reason = "Admin ban reason")
            => new ResolveReportCommand
            {
                Id = reportId,
                IsBanned = isBanned,
                Duration = duration,
                Reason = reason
            };

        private static ReportUserCommand CreateCommand(Guid reportedUserId, string reason = "Spam")
            => new ReportUserCommand
            {
                UserId = reportedUserId,
                Reason = reason
            };

        private static readonly DateTime BaseDate = new(2025, 1, 10, 12, 0, 0, DateTimeKind.Utc);

        private static ReportStatusEnum NonPendingStatus
            => Enum.GetValues<ReportStatusEnum>().First(s => s != ReportStatusEnum.Pending);

        private async Task<UserReport> AddReportAsync(
            User reported,
            User reporter,
            string reason = "Spam content",
            ReportStatusEnum status = ReportStatusEnum.Pending,
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

            _context.UserReports.Add(report);
            await _context.SaveChangesAsync();

            var created = createdAt ?? BaseDate;
            await _context.UserReports
                .Where(r => r.Id == report.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.CreatedAt, created));

            await _context.Entry(report).ReloadAsync();

            return report;
        }

        private static ReportListCommand CreateListCommand(
            bool isShowAll = false,
            string? sortBy = null,
            bool sortDescending = false,
            string? searchTerm = null,
            int pageNumber = 1,
            int pageSize = 10)
            => new ReportListCommand
            {
                IsShowAll = isShowAll,
                SortBy = sortBy,
                SortDescending = sortDescending,
                SearchTerm = searchTerm,
                PageNumber = pageNumber,
                PageSize = pageSize
            };

        private async Task<User> AddAdminUserAsync(
            string firstName = "Admin",
            string lastName = "User")
        {
            var user = await AddUserAsync(firstName, lastName);

            var adminRole = await _context.Roles.FirstOrDefaultAsync(r => r.NormalizedName == "ADMIN");
            if (adminRole == null)
            {
                adminRole = new IdentityRole<Guid>
                {
                    Id = Guid.NewGuid(),
                    Name = "Admin",
                    NormalizedName = "ADMIN"
                };
                _context.Roles.Add(adminRole);
            }

            _context.UserRoles.Add(new IdentityUserRole<Guid>
            {
                UserId = user.Id,
                RoleId = adminRole.Id
            });

            await _context.SaveChangesAsync();
            return user;
        }
        #endregion

        #region ReportUserAsync 
        [Test]
        public async Task ReportUserAsync_WhenCommandIsValid_ShouldReturnSuccessResult()
        {
            // Arrange
            var reporter = await AddUserAsync();
            var reported = await AddUserAsync("Anna", "Nowak");

            // Act
            var result = await _sut.ReportUserAsync(CreateCommand(reported.Id), reporter.Id);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status201Created);
            await Assert.That(result.Message).IsEqualTo("User reported successfully.");
        }

        [Test]
        public async Task ReportUserAsync_WhenCommandIsValid_ShouldPersistReportWithCorrectData()
        {
            // Arrange
            var reporter = await AddUserAsync();
            var reported = await AddUserAsync("Anna", "Nowak");

            // Act
            await _sut.ReportUserAsync(CreateCommand(reported.Id, "Harassment"), reporter.Id);

            // Assert
            var report = await _context.UserReports.AsNoTracking().SingleAsync();
            await Assert.That(report.ReporterUserId).IsEqualTo(reporter.Id);
            await Assert.That(report.ReportedUserId).IsEqualTo(reported.Id);
            await Assert.That(report.Reason).IsEqualTo("Harassment");
        }

        [Test]
        public async Task ReportUserAsync_WhenSameUserIsReportedTwice_ShouldPersistBothReports()
        {
            // Arrange
            var reporter = await AddUserAsync();
            var reported = await AddUserAsync("Anna", "Nowak");

            // Act
            var first = await _sut.ReportUserAsync(CreateCommand(reported.Id, "Spam"), reporter.Id);
            var second = await _sut.ReportUserAsync(CreateCommand(reported.Id, "Harassment"), reporter.Id);

            // Assert
            await Assert.That(first.IsSuccess).IsTrue();
            await Assert.That(second.IsSuccess).IsTrue();

            var reasons = await _context.UserReports
                .AsNoTracking()
                .Where(r => r.ReportedUserId == reported.Id)
                .Select(r => r.Reason)
                .ToListAsync();

            await Assert.That(reasons.Count).IsEqualTo(2);
            await Assert.That(reasons).Contains("Spam");
            await Assert.That(reasons).Contains("Harassment");
        }

        [Test]
        public async Task ReportUserAsync_WhenDifferentUsersReportTheSameUser_ShouldPersistReportForEachReporter()
        {
            // Arrange
            var reporterA = await AddUserAsync("Adam", "A");
            var reporterB = await AddUserAsync("Beata", "B");
            var reported = await AddUserAsync("Anna", "Nowak");

            // Act
            await _sut.ReportUserAsync(CreateCommand(reported.Id), reporterA.Id);
            await _sut.ReportUserAsync(CreateCommand(reported.Id), reporterB.Id);

            // Assert
            var reporterIds = await _context.UserReports
                .AsNoTracking()
                .Where(r => r.ReportedUserId == reported.Id)
                .Select(r => r.ReporterUserId)
                .ToListAsync();

            await Assert.That(reporterIds.Count).IsEqualTo(2);
            await Assert.That(reporterIds).Contains(reporterA.Id);
            await Assert.That(reporterIds).Contains(reporterB.Id);
        }

        [Test]
        public async Task ReportUserAsync_WhenCommandIsValid_ShouldNotCreateReportsForOtherUsers()
        {
            // Arrange
            var reporter = await AddUserAsync();
            var reported = await AddUserAsync("Anna", "Nowak");
            var bystander = await AddUserAsync("Piotr", "Zielinski");

            // Act
            await _sut.ReportUserAsync(CreateCommand(reported.Id), reporter.Id);

            // Assert
            var anyAgainstBystander = await _context.UserReports
                .AnyAsync(r => r.ReportedUserId == bystander.Id || r.ReporterUserId == bystander.Id);

            await Assert.That(anyAgainstBystander).IsFalse();
        }

        [Test]
        public async Task ReportUserAsync_WhenUserReportsThemselves_ShouldReturnBadRequestFailure()
        {
            // Arrange
            var user = await AddUserAsync();

            // Act
            var result = await _sut.ReportUserAsync(CreateCommand(user.Id), user.Id);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.InvalidOperation);
            await Assert.That(result.Message).IsEqualTo("You cannot report yourself.");
        }

        [Test]
        public async Task ReportUserAsync_WhenUserReportsThemselves_ShouldNotPersistReport()
        {
            // Arrange
            var user = await AddUserAsync();

            // Act
            await _sut.ReportUserAsync(CreateCommand(user.Id), user.Id);

            // Assert
            await Assert.That(await _context.UserReports.AnyAsync()).IsFalse();
        }

        [Test]
        public async Task ReportUserAsync_WhenUserReportsThemselvesAndDoesNotExist_ShouldStillReturnSelfReportFailure()
        {
            // Arrange
            var userId = Guid.NewGuid();

            // Act
            var result = await _sut.ReportUserAsync(CreateCommand(userId), userId);

            // Assert
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.InvalidOperation);
        }

        [Test]
        public async Task ReportUserAsync_WhenReportedUserDoesNotExist_ShouldReturnNotFoundFailure()
        {
            // Arrange
            var reporter = await AddUserAsync();
            var missingUserId = Guid.NewGuid();

            // Act
            var result = await _sut.ReportUserAsync(CreateCommand(missingUserId), reporter.Id);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status404NotFound);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.NotFound);
            await Assert.That(result.Message).IsEqualTo("The user you are trying to report does not exist.");
        }

        [Test]
        public async Task ReportUserAsync_WhenReportedUserDoesNotExist_ShouldNotPersistReport()
        {
            // Arrange
            var reporter = await AddUserAsync();

            // Act
            await _sut.ReportUserAsync(CreateCommand(Guid.NewGuid()), reporter.Id);

            // Assert
            await Assert.That(await _context.UserReports.AnyAsync()).IsFalse();
        }

        [Test]
        public async Task ReportUserAsync_WhenReportedUserIsEmptyGuid_ShouldReturnNotFoundFailure()
        {
            // Arrange
            var reporter = await AddUserAsync();

            // Act
            var result = await _sut.ReportUserAsync(CreateCommand(Guid.Empty), reporter.Id);

            // Assert
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status404NotFound);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.NotFound);
        }

        [Test]
        public async Task ReportUserAsync_WhenReportedUserIsSoftDeleted_ShouldReturnNotFoundFailure()
        {
            // Arrange
            var reporter = await AddUserAsync();
            var reported = await AddUserAsync("Anna", "Nowak");

            _context.Remove(reported);
            await _context.SaveChangesAsync();

            // Act
            var result = await _sut.ReportUserAsync(CreateCommand(reported.Id), reporter.Id);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status404NotFound);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.NotFound);
            await Assert.That(await _context.UserReports.AnyAsync()).IsFalse();
        }

        [Test]
        public async Task ReportUserAsync_WhenReportedUserIsAdmin_ShouldReturnBadRequestFailure()
        {
            // Arrange
            var reporter = await AddUserAsync();
            var adminTarget = await AddAdminUserAsync("Super", "Admin");

            // Act
            var result = await _sut.ReportUserAsync(CreateCommand(adminTarget.Id), reporter.Id);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.InvalidOperation);
            await Assert.That(result.Message).IsEqualTo("You cannot report an administrator.");
            await Assert.That(await _context.UserReports.AnyAsync()).IsFalse();
        }
        #endregion

        #region GetReportListAsync
        [Test]
        public async Task GetReportListAsync_WhenIsShowAllIsFalse_ShouldReturnOnlyPendingReports()
        {
            // Arrange
            var reporter = await AddUserAsync("Adam", "Reporter");
            var reported = await AddUserAsync("Anna", "Nowak");
            var pending = await AddReportAsync(reported, reporter, status: ReportStatusEnum.Pending);
            await AddReportAsync(reported, reporter, status: NonPendingStatus, resolvedAt: BaseDate.AddDays(1));

            // Act
            var result = await _sut.GetReportListAsync(CreateListCommand(isShowAll: false));

            // Assert
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            var item = result.Data!.Items.Single();
            await Assert.That(item.Id).IsEqualTo(pending.Id);
            await Assert.That(item.Status).IsEqualTo(ReportStatusEnum.Pending);
        }

        [Test]
        public async Task GetReportListAsync_WhenIsShowAllIsTrue_ShouldReturnReportsWithAllStatuses()
        {
            // Arrange
            var reporter = await AddUserAsync("Adam", "Reporter");
            var reported = await AddUserAsync("Anna", "Nowak");
            await AddReportAsync(reported, reporter, status: ReportStatusEnum.Pending);
            await AddReportAsync(reported, reporter, status: NonPendingStatus, resolvedAt: BaseDate.AddDays(1));

            // Act
            var result = await _sut.GetReportListAsync(CreateListCommand(isShowAll: true));

            // Assert
            await Assert.That(result.Data!.Items.Count).IsEqualTo(2);
            await Assert.That(result.Data!.TotalCount).IsEqualTo(2);
        }

        [Test]
        public async Task GetReportListAsync_WhenThereAreNoReports_ShouldReturnEmptyList()
        {
            // Act
            var result = await _sut.GetReportListAsync(CreateListCommand(isShowAll: true));

            // Assert
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(result.Data!.Items).IsEmpty();
            await Assert.That(result.Data!.TotalCount).IsEqualTo(0);
        }

        [Test]
        public async Task GetReportListAsync_WhenOnlyResolvedReportsExistAndIsShowAllIsFalse_ShouldReturnEmptyList()
        {
            // Arrange
            var reporter = await AddUserAsync("Adam", "Reporter");
            var reported = await AddUserAsync("Anna", "Nowak");
            await AddReportAsync(reported, reporter, status: NonPendingStatus, resolvedAt: BaseDate.AddDays(1));

            // Act
            var result = await _sut.GetReportListAsync(CreateListCommand(isShowAll: false));

            // Assert
            await Assert.That(result.Data!.Items).IsEmpty();
        }
        #endregion

        #region GetReportListAsync 
        [Test]
        public async Task GetReportListAsync_ShouldMapReportAndReportedUserData()
        {
            // Arrange
            var reporter = await AddUserAsync("Adam", "Reporter");
            var reported = await AddUserAsync("Anna", "Nowak");
            var report = await AddReportAsync(reported, reporter, "Harassment", ReportStatusEnum.Pending, BaseDate);

            // Act
            var result = await _sut.GetReportListAsync(CreateListCommand());

            // Assert
            var item = result.Data!.Items.Single();
            await Assert.That(item.Id).IsEqualTo(report.Id);
            await Assert.That(item.ReportedUserId).IsEqualTo(reported.Id);
            await Assert.That(item.ReportedFirstName).IsEqualTo("Anna");
            await Assert.That(item.ReportedLastName).IsEqualTo("Nowak");
            await Assert.That(item.Reason).IsEqualTo("Harassment");
            await Assert.That(item.Status).IsEqualTo(ReportStatusEnum.Pending);
            await Assert.That(item.CreatedAt).IsEqualTo(report.CreatedAt);
            await Assert.That(item.ResolvedAt).IsNull();
        }

        [Test]
        public async Task GetReportListAsync_WhenReportIsResolved_ShouldMapResolvedAt()
        {
            // Arrange
            var reporter = await AddUserAsync("Adam", "Reporter");
            var reported = await AddUserAsync("Anna", "Nowak");
            var report = await AddReportAsync(
                reported, reporter, status: NonPendingStatus, resolvedAt: BaseDate.AddDays(2));

            // Act
            var result = await _sut.GetReportListAsync(CreateListCommand(isShowAll: true));

            // Assert
            var item = result.Data!.Items.Single();
            await Assert.That(item.Status).IsEqualTo(NonPendingStatus);
            await Assert.That(item.ResolvedAt).IsEqualTo(report.ResolvedAt);
        }

        [Test]
        public async Task GetReportListAsync_WhenReasonIsShorterThanLimit_ShouldNotTruncateReason()
        {
            // Arrange
            var reporter = await AddUserAsync("Adam", "Reporter");
            var reported = await AddUserAsync("Anna", "Nowak");
            var reason = new string('a', 49);
            await AddReportAsync(reported, reporter, reason);

            // Act
            var result = await _sut.GetReportListAsync(CreateListCommand());

            // Assert
            await Assert.That(result.Data!.Items.Single().Reason).IsEqualTo(reason);
        }

        [Test]
        public async Task GetReportListAsync_WhenReasonHasExactlyLimitLength_ShouldNotTruncateReason()
        {
            // Arrange
            var reporter = await AddUserAsync("Adam", "Reporter");
            var reported = await AddUserAsync("Anna", "Nowak");
            var reason = new string('a', 50);
            await AddReportAsync(reported, reporter, reason);

            // Act
            var result = await _sut.GetReportListAsync(CreateListCommand());

            // Assert
            await Assert.That(result.Data!.Items.Single().Reason).IsEqualTo(reason);
        }

        [Test]
        public async Task GetReportListAsync_WhenReasonIsLongerThanLimit_ShouldTruncateReasonAndAppendEllipsis()
        {
            // Arrange
            var reporter = await AddUserAsync("Adam", "Reporter");
            var reported = await AddUserAsync("Anna", "Nowak");
            await AddReportAsync(reported, reporter, new string('a', 51));

            // Act
            var result = await _sut.GetReportListAsync(CreateListCommand());

            // Assert
            await Assert.That(result.Data!.Items.Single().Reason).IsEqualTo(new string('a', 50) + "...");
        }

        [Test]
        public async Task GetReportListAsync_WhenSortByIsNull_ShouldSortByCreatedAtDescending()
        {
            // Arrange
            var reporter = await AddUserAsync("Adam", "Reporter");
            var reported = await AddUserAsync("Anna", "Nowak");
            var oldest = await AddReportAsync(reported, reporter, createdAt: BaseDate);
            var middle = await AddReportAsync(reported, reporter, createdAt: BaseDate.AddDays(1));
            var newest = await AddReportAsync(reported, reporter, createdAt: BaseDate.AddDays(2));

            // Act
            var result = await _sut.GetReportListAsync(CreateListCommand(sortBy: null));

            // Assert
            var ids = result.Data!.Items.Select(i => i.Id).ToList();
            await Assert.That(ids.SequenceEqual(new[] { newest.Id, middle.Id, oldest.Id })).IsTrue();
        }

        [Test]
        public async Task GetReportListAsync_WhenSortByIsUnknown_ShouldFallBackToCreatedAtDescending()
        {
            // Arrange
            var reporter = await AddUserAsync("Adam", "Reporter");
            var reported = await AddUserAsync("Anna", "Nowak");
            var oldest = await AddReportAsync(reported, reporter, createdAt: BaseDate);
            var newest = await AddReportAsync(reported, reporter, createdAt: BaseDate.AddDays(1));

            // Act
            var result = await _sut.GetReportListAsync(CreateListCommand(sortBy: "doesNotExist", sortDescending: false));

            // Assert
            var ids = result.Data!.Items.Select(i => i.Id).ToList();
            await Assert.That(ids.SequenceEqual(new[] { newest.Id, oldest.Id })).IsTrue();
        }

        [Test]
        public async Task GetReportListAsync_WhenSortByCreatedAtAscending_ShouldReturnOldestFirst()
        {
            // Arrange
            var reporter = await AddUserAsync("Adam", "Reporter");
            var reported = await AddUserAsync("Anna", "Nowak");
            var newest = await AddReportAsync(reported, reporter, createdAt: BaseDate.AddDays(2));
            var oldest = await AddReportAsync(reported, reporter, createdAt: BaseDate);
            var middle = await AddReportAsync(reported, reporter, createdAt: BaseDate.AddDays(1));

            // Act
            var result = await _sut.GetReportListAsync(CreateListCommand(sortBy: "createdAt", sortDescending: false));

            // Assert
            var ids = result.Data!.Items.Select(i => i.Id).ToList();
            await Assert.That(ids.SequenceEqual(new[] { oldest.Id, middle.Id, newest.Id })).IsTrue();
        }

        [Test]
        public async Task GetReportListAsync_WhenSortByCreatedAtDescending_ShouldReturnNewestFirst()
        {
            // Arrange
            var reporter = await AddUserAsync("Adam", "Reporter");
            var reported = await AddUserAsync("Anna", "Nowak");
            var oldest = await AddReportAsync(reported, reporter, createdAt: BaseDate);
            var newest = await AddReportAsync(reported, reporter, createdAt: BaseDate.AddDays(2));
            var middle = await AddReportAsync(reported, reporter, createdAt: BaseDate.AddDays(1));

            // Act
            var result = await _sut.GetReportListAsync(CreateListCommand(sortBy: "createdAt", sortDescending: true));

            // Assert
            var ids = result.Data!.Items.Select(i => i.Id).ToList();
            await Assert.That(ids.SequenceEqual(new[] { newest.Id, middle.Id, oldest.Id })).IsTrue();
        }

        [Test]
        public async Task GetReportListAsync_WhenSortByIsDifferentCase_ShouldStillApplySorting()
        {
            // Arrange
            var reporter = await AddUserAsync("Adam", "Reporter");
            var reported = await AddUserAsync("Anna", "Nowak");
            var oldest = await AddReportAsync(reported, reporter, createdAt: BaseDate);
            var newest = await AddReportAsync(reported, reporter, createdAt: BaseDate.AddDays(1));

            // Act
            var result = await _sut.GetReportListAsync(CreateListCommand(sortBy: "CREATEDAT", sortDescending: false));

            // Assert
            var ids = result.Data!.Items.Select(i => i.Id).ToList();
            await Assert.That(ids.SequenceEqual(new[] { oldest.Id, newest.Id })).IsTrue();
        }

        [Test]
        public async Task GetReportListAsync_WhenSortByResolvedAt_ShouldSortByResolvedAtInRequestedDirection()
        {
            // Arrange
            var reporter = await AddUserAsync("Adam", "Reporter");
            var reported = await AddUserAsync("Anna", "Nowak");
            var resolvedFirst = await AddReportAsync(
                reported, reporter, status: NonPendingStatus, resolvedAt: BaseDate.AddDays(1));
            var resolvedLast = await AddReportAsync(
                reported, reporter, status: NonPendingStatus, resolvedAt: BaseDate.AddDays(5));

            // Act
            var ascending = await _sut.GetReportListAsync(
                CreateListCommand(isShowAll: true, sortBy: "resolvedAt", sortDescending: false));
            var descending = await _sut.GetReportListAsync(
                CreateListCommand(isShowAll: true, sortBy: "resolvedAt", sortDescending: true));

            // Assert
            await Assert.That(ascending.Data!.Items.Select(i => i.Id).SequenceEqual(
                new[] { resolvedFirst.Id, resolvedLast.Id })).IsTrue();
            await Assert.That(descending.Data!.Items.Select(i => i.Id).SequenceEqual(
                new[] { resolvedLast.Id, resolvedFirst.Id })).IsTrue();
        }

        [Test]
        public async Task GetReportListAsync_WhenSortByStatus_ShouldSortByStatusInRequestedDirection()
        {
            // Arrange
            var reporter = await AddUserAsync("Adam", "Reporter");
            var reported = await AddUserAsync("Anna", "Nowak");
            await AddReportAsync(reported, reporter, status: ReportStatusEnum.Pending, createdAt: BaseDate.AddDays(2));
            await AddReportAsync(reported, reporter, status: NonPendingStatus,
                createdAt: BaseDate.AddDays(1), resolvedAt: BaseDate.AddDays(3));

            var expectedAscending = await _context.UserReports
                .AsNoTracking()
                .OrderBy(r => r.Status)
                .Select(r => r.Id)
                .ToListAsync();

            // Act
            var ascending = await _sut.GetReportListAsync(
                CreateListCommand(isShowAll: true, sortBy: "status", sortDescending: false));
            var descending = await _sut.GetReportListAsync(
                CreateListCommand(isShowAll: true, sortBy: "status", sortDescending: true));

            // Assert
            await Assert.That(ascending.Data!.Items.Select(i => i.Id).SequenceEqual(expectedAscending)).IsTrue();
            await Assert.That(descending.Data!.Items.Select(i => i.Id)
                .SequenceEqual(Enumerable.Reverse(expectedAscending))).IsTrue();
        }

        [Test]
        public async Task GetReportListAsync_WhenSearchTermIsNull_ShouldReturnAllMatchingFilter()
        {
            // Arrange
            var reporter = await AddUserAsync("Adam", "Reporter");
            var anna = await AddUserAsync("Anna", "Nowak");
            var piotr = await AddUserAsync("Piotr", "Zielinski");
            await AddReportAsync(anna, reporter);
            await AddReportAsync(piotr, reporter);

            // Act
            var result = await _sut.GetReportListAsync(CreateListCommand(searchTerm: null));

            // Assert
            await Assert.That(result.Data!.Items.Count).IsEqualTo(2);
        }

        [Test]
        public async Task GetReportListAsync_WhenSearchTermIsWhitespace_ShouldIgnoreSearch()
        {
            // Arrange
            var reporter = await AddUserAsync("Adam", "Reporter");
            var anna = await AddUserAsync("Anna", "Nowak");
            var piotr = await AddUserAsync("Piotr", "Zielinski");
            await AddReportAsync(anna, reporter);
            await AddReportAsync(piotr, reporter);

            // Act
            var result = await _sut.GetReportListAsync(CreateListCommand(searchTerm: "    "));

            // Assert
            await Assert.That(result.Data!.Items.Count).IsEqualTo(2);
        }

        [Test]
        public async Task GetReportListAsync_WhenSearchingByReportedUserFirstName_ShouldReturnMatchingReports()
        {
            // Arrange
            var reporter = await AddUserAsync("Adam", "Reporter");
            var anna = await AddUserAsync("Anna", "Nowak");
            var piotr = await AddUserAsync("Piotr", "Zielinski");
            var annaReport = await AddReportAsync(anna, reporter);
            await AddReportAsync(piotr, reporter);

            // Act
            var result = await _sut.GetReportListAsync(CreateListCommand(searchTerm: "Anna"));

            // Assert
            await Assert.That(result.Data!.Items.Single().Id).IsEqualTo(annaReport.Id);
        }

        [Test]
        public async Task GetReportListAsync_WhenSearchingByReportedUserLastName_ShouldReturnMatchingReports()
        {
            // Arrange
            var reporter = await AddUserAsync("Adam", "Reporter");
            var anna = await AddUserAsync("Anna", "Nowak");
            var piotr = await AddUserAsync("Piotr", "Zielinski");
            await AddReportAsync(anna, reporter);
            var piotrReport = await AddReportAsync(piotr, reporter);

            // Act
            var result = await _sut.GetReportListAsync(CreateListCommand(searchTerm: "Zielinski"));

            // Assert
            await Assert.That(result.Data!.Items.Single().Id).IsEqualTo(piotrReport.Id);
        }

        [Test]
        public async Task GetReportListAsync_WhenSearchingByReason_ShouldReturnMatchingReports()
        {
            // Arrange
            var reporter = await AddUserAsync("Adam", "Reporter");
            var reported = await AddUserAsync("Anna", "Nowak");
            var spam = await AddReportAsync(reported, reporter, "Sends spam messages");
            await AddReportAsync(reported, reporter, "Offensive profile picture");

            // Act
            var result = await _sut.GetReportListAsync(CreateListCommand(searchTerm: "spam"));

            // Assert
            await Assert.That(result.Data!.Items.Single().Id).IsEqualTo(spam.Id);
        }

        [Test]
        public async Task GetReportListAsync_WhenSearchingByPartOfWord_ShouldReturnMatchingReports()
        {
            // Arrange
            var reporter = await AddUserAsync("Adam", "Reporter");
            var reported = await AddUserAsync("Anna", "Nowak");
            var report = await AddReportAsync(reported, reporter);

            // Act
            var result = await _sut.GetReportListAsync(CreateListCommand(searchTerm: "now"));

            // Assert
            await Assert.That(result.Data!.Items.Single().Id).IsEqualTo(report.Id);
        }

        [Test]
        public async Task GetReportListAsync_WhenSearchTermHasDifferentCase_ShouldBeCaseInsensitive()
        {
            // Arrange
            var reporter = await AddUserAsync("Adam", "Reporter");
            var reported = await AddUserAsync("Anna", "Nowak");
            var report = await AddReportAsync(reported, reporter);

            // Act
            var result = await _sut.GetReportListAsync(CreateListCommand(searchTerm: "aNNa"));

            // Assert
            await Assert.That(result.Data!.Items.Single().Id).IsEqualTo(report.Id);
        }

        [Test]
        public async Task GetReportListAsync_WhenSearchTermHasNoDiacritics_ShouldMatchAccentedNames()
        {
            // Arrange
            var reporter = await AddUserAsync("Adam", "Reporter");
            var reported = await AddUserAsync("José", "Łukasiewicz");
            var report = await AddReportAsync(reported, reporter);

            // Act
            var byFirstName = await _sut.GetReportListAsync(CreateListCommand(searchTerm: "jose"));
            var byLastName = await _sut.GetReportListAsync(CreateListCommand(searchTerm: "lukasiewicz"));

            // Assert
            await Assert.That(byFirstName.Data!.Items.Single().Id).IsEqualTo(report.Id);
            await Assert.That(byLastName.Data!.Items.Single().Id).IsEqualTo(report.Id);
        }

        [Test]
        public async Task GetReportListAsync_WhenSearchHasMultipleTerms_ShouldRequireAllTermsToMatch()
        {
            // Arrange
            var reporter = await AddUserAsync("Adam", "Reporter");
            var anna = await AddUserAsync("Anna", "Nowak");
            var piotr = await AddUserAsync("Piotr", "Nowak");
            var matching = await AddReportAsync(anna, reporter, "Sends spam messages");
            await AddReportAsync(anna, reporter, "Offensive profile picture");
            await AddReportAsync(piotr, reporter, "Sends spam messages");

            // Act
            var result = await _sut.GetReportListAsync(CreateListCommand(searchTerm: "Anna spam"));

            // Assert
            await Assert.That(result.Data!.Items.Single().Id).IsEqualTo(matching.Id);
        }

        [Test]
        public async Task GetReportListAsync_WhenSearchHasExtraSpacesBetweenTerms_ShouldStillMatch()
        {
            // Arrange
            var reporter = await AddUserAsync("Adam", "Reporter");
            var reported = await AddUserAsync("Anna", "Nowak");
            var report = await AddReportAsync(reported, reporter);

            // Act
            var result = await _sut.GetReportListAsync(CreateListCommand(searchTerm: "  Anna    Nowak  "));

            // Assert
            await Assert.That(result.Data!.Items.Single().Id).IsEqualTo(report.Id);
        }

        [Test]
        public async Task GetReportListAsync_WhenNothingMatchesSearchTerm_ShouldReturnEmptyList()
        {
            // Arrange
            var reporter = await AddUserAsync("Adam", "Reporter");
            var reported = await AddUserAsync("Anna", "Nowak");
            await AddReportAsync(reported, reporter);

            // Act
            var result = await _sut.GetReportListAsync(CreateListCommand(searchTerm: "xyz"));

            // Assert
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(result.Data!.Items).IsEmpty();
            await Assert.That(result.Data!.TotalCount).IsEqualTo(0);
        }

        [Test]
        public async Task GetReportListAsync_WhenSearchingByReporterName_ShouldNotMatchReporter()
        {
            // Arrange
            var reporter = await AddUserAsync("Zbigniew", "Reporter");
            var reported = await AddUserAsync("Anna", "Nowak");
            await AddReportAsync(reported, reporter);

            // Act
            var result = await _sut.GetReportListAsync(CreateListCommand(searchTerm: "Zbigniew"));

            // Assert
            await Assert.That(result.Data!.Items).IsEmpty();
        }

        [Test]
        public async Task GetReportListAsync_WhenSearchAndFilterAreCombined_ShouldApplyBoth()
        {
            // Arrange
            var reporter = await AddUserAsync("Adam", "Reporter");
            var anna = await AddUserAsync("Anna", "Nowak");
            var pending = await AddReportAsync(anna, reporter, status: ReportStatusEnum.Pending);
            await AddReportAsync(anna, reporter, status: NonPendingStatus, resolvedAt: BaseDate.AddDays(1));

            // Act
            var result = await _sut.GetReportListAsync(CreateListCommand(isShowAll: false, searchTerm: "Anna"));

            // Assert
            await Assert.That(result.Data!.Items.Single().Id).IsEqualTo(pending.Id);
        }

        [Test]
        public async Task GetReportListAsync_WhenResultsExceedPageSize_ShouldReturnRequestedPageOnly()
        {
            // Arrange
            var reporter = await AddUserAsync("Adam", "Reporter");
            var reported = await AddUserAsync("Anna", "Nowak");
            for (var i = 0; i < 5; i++)
                await AddReportAsync(reported, reporter, createdAt: BaseDate.AddDays(i));

            // Act
            var page1 = await _sut.GetReportListAsync(CreateListCommand(pageNumber: 1, pageSize: 2));
            var page2 = await _sut.GetReportListAsync(CreateListCommand(pageNumber: 2, pageSize: 2));
            var page3 = await _sut.GetReportListAsync(CreateListCommand(pageNumber: 3, pageSize: 2));

            // Assert
            await Assert.That(page1.Data!.Items.Count).IsEqualTo(2);
            await Assert.That(page2.Data!.Items.Count).IsEqualTo(2);
            await Assert.That(page3.Data!.Items.Count).IsEqualTo(1);
            await Assert.That(page1.Data!.TotalCount).IsEqualTo(5);

            var allIds = page1.Data.Items.Concat(page2.Data.Items).Concat(page3.Data.Items).Select(i => i.Id).ToList();
            await Assert.That(allIds.Distinct().Count()).IsEqualTo(5);
        }

        [Test]
        public async Task GetReportListAsync_WhenPageIsBeyondLastPage_ShouldReturnLastPage()
        {
            // Arrange
            var reporter = await AddUserAsync("Adam", "Reporter");
            var reported = await AddUserAsync("Anna", "Nowak");
            var report = await AddReportAsync(reported, reporter);

            // Act
            var result = await _sut.GetReportListAsync(CreateListCommand(pageNumber: 5, pageSize: 10));

            // Assert
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(result.Data!.Items.Single().Id).IsEqualTo(report.Id);
        }

        [Test]
        public async Task GetReportListAsync_WhenPagingWithSorting_ShouldPageAfterSorting()
        {
            // Arrange
            var reporter = await AddUserAsync("Adam", "Reporter");
            var reported = await AddUserAsync("Anna", "Nowak");
            var oldest = await AddReportAsync(reported, reporter, createdAt: BaseDate);
            await AddReportAsync(reported, reporter, createdAt: BaseDate.AddDays(1));
            var newest = await AddReportAsync(reported, reporter, createdAt: BaseDate.AddDays(2));

            // Act
            var first = await _sut.GetReportListAsync(
                CreateListCommand(sortBy: "createdAt", sortDescending: false, pageNumber: 1, pageSize: 1));
            var last = await _sut.GetReportListAsync(
                CreateListCommand(sortBy: "createdAt", sortDescending: false, pageNumber: 3, pageSize: 1));

            // Assert
            await Assert.That(first.Data!.Items.Single().Id).IsEqualTo(oldest.Id);
            await Assert.That(last.Data!.Items.Single().Id).IsEqualTo(newest.Id);
        }
        #endregion

        #region ResolveReportAsync

        [Test]
        public async Task ResolveReportAsync_WhenReportDoesNotExist_ShouldReturnNotFoundFailure()
        {
            // Arrange
            var admin = await AddUserAsync("Admin", "User");
            var command = CreateResolveCommand(Guid.NewGuid(), isBanned: false);

            // Act
            var result = await _sut.ResolveReportAsync(command, admin.Id);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status404NotFound);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.ReportNotFound);
            await Assert.That(result.Message).IsEqualTo("Report does not exist.");
        }

        [Test]
        [Arguments(ReportStatusEnum.Approved)]
        [Arguments(ReportStatusEnum.Rejected)]
        public async Task ResolveReportAsync_WhenReportIsAlreadyResolved_ShouldReturnBadRequestFailure(ReportStatusEnum existingStatus)
        {
            // Arrange
            var admin = await AddUserAsync("Admin", "User");
            var reporter = await AddUserAsync("Adam", "Reporter");
            var reported = await AddUserAsync("Anna", "Nowak");
            var report = await AddReportAsync(reported, reporter, status: existingStatus, resolvedAt: BaseDate);

            var command = CreateResolveCommand(report.Id, isBanned: true, duration: TimeSpan.FromDays(1));

            // Act
            var result = await _sut.ResolveReportAsync(command, admin.Id);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.ReportAlreadyResolved);
            await Assert.That(result.Message).IsEqualTo("Report has already been resolved.");
        }

        [Test]
        public async Task ResolveReportAsync_WhenIsBannedIsFalse_ShouldRejectReportAndNotBanUser()
        {
            // Arrange
            var admin = await AddUserAsync("Admin", "User");
            var reporter = await AddUserAsync("Adam", "Reporter");
            var reported = await AddUserAsync("Anna", "Nowak");
            var report = await AddReportAsync(reported, reporter, reason: "False accusation");

            var beforeResolve = DateTime.UtcNow;
            var command = CreateResolveCommand(report.Id, isBanned: false);

            // Act
            var result = await _sut.ResolveReportAsync(command, admin.Id);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(result.Message).IsEqualTo("Report resolved successfully.");

            var updatedReport = await _context.UserReports.AsNoTracking().SingleAsync(r => r.Id == report.Id);
            await Assert.That(updatedReport.Status).IsEqualTo(ReportStatusEnum.Rejected);
            await Assert.That(updatedReport.ResolvedByAdminId).IsEqualTo(admin.Id);
            await Assert.That(updatedReport.ResolvedAt).IsNotNull();
            await Assert.That(updatedReport.ResolvedAt!.Value).IsGreaterThanOrEqualTo(beforeResolve);

            var updatedUser = await _context.Users.AsNoTracking().SingleAsync(u => u.Id == reported.Id);
            await Assert.That(updatedUser.IsBanned).IsFalse();
            await Assert.That(updatedUser.BannedUntil).IsNull();
            await Assert.That(updatedUser.BannedReason).IsNull();
            await Assert.That(updatedUser.LockoutEnd).IsNull();
        }

        [Test]
        public async Task ResolveReportAsync_WhenIsBannedIsTrueAndDurationIsNull_ShouldApproveReportAndApplyPermanentBan()
        {
            // Arrange
            var admin = await AddUserAsync("Admin", "User");
            var reporter = await AddUserAsync("Adam", "Reporter");
            var reported = await AddUserAsync("Anna", "Nowak");
            var initialSecurityStamp = reported.SecurityStamp;
            var report = await AddReportAsync(reported, reporter, reason: "Severe harassment");

            var command = CreateResolveCommand(report.Id, isBanned: true, duration: null, reason: "Severe harassment");

            // Act
            var result = await _sut.ResolveReportAsync(command, admin.Id);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);

            var updatedReport = await _context.UserReports.AsNoTracking().SingleAsync(r => r.Id == report.Id);
            await Assert.That(updatedReport.Status).IsEqualTo(ReportStatusEnum.Approved);
            await Assert.That(updatedReport.ResolvedByAdminId).IsEqualTo(admin.Id);
            await Assert.That(updatedReport.ResolvedAt).IsNotNull();

            var updatedUser = await _context.Users.AsNoTracking().SingleAsync(u => u.Id == reported.Id);
            await Assert.That(updatedUser.IsBanned).IsTrue();
            await Assert.That(updatedUser.BannedUntil).IsNull();
            await Assert.That(updatedUser.BannedReason).IsEqualTo("Severe harassment");
        }

        [Test]
        public async Task ResolveReportAsync_WhenIsBannedIsTrueAndDurationIsProvided_ShouldApproveReportAndApplyTemporaryBan()
        {
            // Arrange
            var admin = await AddUserAsync("Admin", "User");
            var reporter = await AddUserAsync("Adam", "Reporter");
            var reported = await AddUserAsync("Anna", "Nowak");
            var report = await AddReportAsync(reported, reporter, reason: "Spam links");

            var duration = TimeSpan.FromDays(7);
            var beforeBan = DateTime.UtcNow;
            var command = CreateResolveCommand(report.Id, isBanned: true, duration: duration, reason: "Spam links");

            // Act
            var result = await _sut.ResolveReportAsync(command, admin.Id);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();

            var updatedReport = await _context.UserReports.AsNoTracking().SingleAsync(r => r.Id == report.Id);
            await Assert.That(updatedReport.Status).IsEqualTo(ReportStatusEnum.Approved);

            var updatedUser = await _context.Users.AsNoTracking().SingleAsync(u => u.Id == reported.Id);
            await Assert.That(updatedUser.IsBanned).IsTrue();
            await Assert.That(updatedUser.BannedReason).IsEqualTo("Spam links");
            await Assert.That(updatedUser.BannedUntil).IsNotNull();
            await Assert.That(updatedUser.BannedUntil!.Value).IsGreaterThanOrEqualTo(beforeBan.Add(duration));
            await Assert.That(updatedUser.BannedUntil!.Value).IsLessThanOrEqualTo(DateTime.UtcNow.Add(duration));
        }

        [Test]
        public async Task ResolveReportAsync_WhenReportedUserAlreadyHasActiveBan_ShouldReturnBadRequestAndKeepReportPending()
        {
            // Arrange
            var admin = await AddUserAsync("Admin", "User");
            var reporter = await AddUserAsync("Adam", "Reporter");
            var reported = await AddUserAsync(
                "Anna",
                "Nowak",
                isBanned: true,
                bannedUntil: DateTime.UtcNow.AddDays(3),
                bannedReason: "Previous violation");
            var report = await AddReportAsync(reported, reporter, reason: "New violation");

            var command = CreateResolveCommand(report.Id, isBanned: true, duration: TimeSpan.FromDays(7));

            // Act
            var result = await _sut.ResolveReportAsync(command, admin.Id);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.UserAlreadyBanned);
            await Assert.That(result.Message).IsEqualTo("Reported user is already banned.");

            var unchangedReport = await _context.UserReports.AsNoTracking().SingleAsync(r => r.Id == report.Id);
            await Assert.That(unchangedReport.Status).IsEqualTo(ReportStatusEnum.Pending);
            await Assert.That(unchangedReport.ResolvedByAdminId).IsNull();
            await Assert.That(unchangedReport.ResolvedAt).IsNull();
        }

        [Test]
        public async Task ResolveReportAsync_WhenReportedUserHadExpiredBan_ShouldAllowNewBan()
        {
            // Arrange
            var admin = await AddUserAsync("Admin", "User");
            var reporter = await AddUserAsync("Adam", "Reporter");
            var reported = await AddUserAsync(
                "Anna",
                "Nowak",
                isBanned: true,
                bannedUntil: DateTime.UtcNow.AddDays(-1),
                bannedReason: "Old expired ban");
            var report = await AddReportAsync(reported, reporter, reason: "Repeat offense");

            var command = CreateResolveCommand(report.Id, isBanned: true, duration: TimeSpan.FromDays(14), reason: "Repeat offense");

            // Act
            var result = await _sut.ResolveReportAsync(command, admin.Id);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();

            var updatedReport = await _context.UserReports.AsNoTracking().SingleAsync(r => r.Id == report.Id);
            await Assert.That(updatedReport.Status).IsEqualTo(ReportStatusEnum.Approved);

            var updatedUser = await _context.Users.AsNoTracking().SingleAsync(u => u.Id == reported.Id);
            await Assert.That(updatedUser.IsBanned).IsTrue();
            await Assert.That(updatedUser.BannedReason).IsEqualTo("Repeat offense");
            await Assert.That(updatedUser.BannedUntil!.Value).IsGreaterThan(DateTime.UtcNow);
        }

        [Test]
        public async Task ResolveReportAsync_WhenUserIsBanned_ShouldAlsoApproveOtherPendingReportsForSameUser()
        {
            // Arrange
            var admin = await AddUserAsync("Admin", "User");
            var reporterA = await AddUserAsync("Adam", "Reporter");
            var reporterB = await AddUserAsync("Beata", "Reporter");
            var reported = await AddUserAsync("Anna", "Nowak");
            var otherUser = await AddUserAsync("Piotr", "Zielinski");

            var mainReport = await AddReportAsync(reported, reporterA, reason: "Spam 1");
            var duplicateReport1 = await AddReportAsync(reported, reporterB, reason: "Spam 2");
            var duplicateReport2 = await AddReportAsync(reported, reporterA, reason: "Spam 3");
            var unrelatedReport = await AddReportAsync(otherUser, reporterA, reason: "Other user report");

            var command = CreateResolveCommand(mainReport.Id, isBanned: true, duration: TimeSpan.FromDays(7));

            // Act
            var result = await _sut.ResolveReportAsync(command, admin.Id);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();

            var reports = await _context.UserReports.AsNoTracking().ToListAsync();

            var resolvedMain = reports.Single(r => r.Id == mainReport.Id);
            var resolvedDup1 = reports.Single(r => r.Id == duplicateReport1.Id);
            var resolvedDup2 = reports.Single(r => r.Id == duplicateReport2.Id);
            var untouchedOther = reports.Single(r => r.Id == unrelatedReport.Id);

            await Assert.That(resolvedMain.Status).IsEqualTo(ReportStatusEnum.Approved);
            await Assert.That(resolvedDup1.Status).IsEqualTo(ReportStatusEnum.Approved);
            await Assert.That(resolvedDup1.ResolvedByAdminId).IsEqualTo(admin.Id);
            await Assert.That(resolvedDup1.ResolvedAt).IsNotNull();

            await Assert.That(resolvedDup2.Status).IsEqualTo(ReportStatusEnum.Approved);
            await Assert.That(resolvedDup2.ResolvedByAdminId).IsEqualTo(admin.Id);
            await Assert.That(resolvedDup2.ResolvedAt).IsNotNull();

            await Assert.That(untouchedOther.Status).IsEqualTo(ReportStatusEnum.Pending);
            await Assert.That(untouchedOther.ResolvedByAdminId).IsNull();
            await Assert.That(untouchedOther.ResolvedAt).IsNull();
        }

        [Test]
        public async Task ResolveReportAsync_WhenBannedPermanently_ShouldSendPermanentBanNotificationEmail()
        {
            // Arrange
            var admin = await AddUserAsync("Admin", "User");
            var reporter = await AddUserAsync("Adam", "Reporter");
            var reported = await AddUserAsync("Anna", "Nowak");
            var report = await AddReportAsync(reported, reporter, reason: "Severe harassment");

            var command = CreateResolveCommand(report.Id, isBanned: true, duration: null, reason: "Severe harassment");

            // Act
            var result = await _sut.ResolveReportAsync(command, admin.Id);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();

            var sentEmail = _emailService.SentEmails.SingleOrDefault(e => e.Type == FakeEmailService.AccountBannedNotificationType);
            await Assert.That(sentEmail).IsNotNull();
            await Assert.That(sentEmail!.To).IsEqualTo(reported.Email);
            await Assert.That(sentEmail.Reason).IsEqualTo("Severe harassment");
            await Assert.That(sentEmail.BannedUntil).IsNull();
        }

        [Test]
        public async Task ResolveReportAsync_WhenRejected_ShouldNotSendBanNotificationEmail()
        {
            // Arrange
            var admin = await AddUserAsync("Admin", "User");
            var reporter = await AddUserAsync("Adam", "Reporter");
            var reported = await AddUserAsync("Anna", "Nowak");
            var report = await AddReportAsync(reported, reporter, reason: "False report");

            var command = CreateResolveCommand(report.Id, isBanned: false);

            // Act
            var result = await _sut.ResolveReportAsync(command, admin.Id);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(_emailService.SentEmails).IsEmpty();
        }

        [Test]
        public async Task ResolveReportAsync_WhenReportedUserIsAdmin_ShouldReturnBadRequestFailure()
        {
            // Arrange
            var resolvingAdmin = await AddAdminUserAsync("Admin", "One");
            var reportedAdmin = await AddAdminUserAsync("Admin", "Two");
            var reporter = await AddUserAsync("Adam", "Reporter");
            var report = await AddReportAsync(reportedAdmin, reporter, reason: "Attempted admin ban");

            var command = CreateResolveCommand(report.Id, isBanned: true, duration: TimeSpan.FromDays(1), reason: "Admin ban");

            // Act
            var result = await _sut.ResolveReportAsync(command, resolvingAdmin.Id);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.InvalidOperation);
            await Assert.That(result.Message).IsEqualTo("You cannot ban an administrator.");

            var unchangedUser = await _context.Users.AsNoTracking().SingleAsync(u => u.Id == reportedAdmin.Id);
            await Assert.That(unchangedUser.IsBanned).IsFalse();
        }
        #endregion

        #region UnbanUserAsync

        [Test]
        public async Task UnbanUserAsync_WhenUserDoesNotExist_ShouldReturnNotFoundFailure()
        {
            // Arrange
            var admin = await AddUserAsync("Admin", "User");
            var missingUserId = Guid.NewGuid();

            // Act
            var result = await _sut.UnbanUserAsync(missingUserId, admin.Id);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status404NotFound);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.NotFound);
            await Assert.That(result.Message).IsEqualTo("User does not exist.");
        }

        [Test]
        public async Task UnbanUserAsync_WhenUserIsEmptyGuid_ShouldReturnNotFoundFailure()
        {
            // Arrange
            var admin = await AddUserAsync("Admin", "User");

            // Act
            var result = await _sut.UnbanUserAsync(Guid.Empty, admin.Id);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status404NotFound);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.NotFound);
        }

        [Test]
        public async Task UnbanUserAsync_WhenUserIsNotBanned_ShouldReturnBadRequestFailure()
        {
            // Arrange
            var admin = await AddUserAsync("Admin", "User");
            var user = await AddUserAsync("Anna", "Nowak", isBanned: false);

            // Act
            var result = await _sut.UnbanUserAsync(user.Id, admin.Id);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.UserNotBanned);
            await Assert.That(result.Message).IsEqualTo("User is not currently banned.");
        }

        [Test]
        public async Task UnbanUserAsync_WhenUserTemporaryBanHasAlreadyExpired_ShouldReturnBadRequestFailure()
        {
            // Arrange
            var admin = await AddUserAsync("Admin", "User");
            var user = await AddUserAsync(
                "Anna",
                "Nowak",
                isBanned: true,
                bannedUntil: DateTime.UtcNow.AddMinutes(-5),
                bannedReason: "Expired ban");

            // Act
            var result = await _sut.UnbanUserAsync(user.Id, admin.Id);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.UserNotBanned);
        }

        [Test]
        public async Task UnbanUserAsync_WhenUserHasActiveTemporaryBan_ShouldClearBanFieldsAndLockout()
        {
            // Arrange
            var admin = await AddUserAsync("Admin", "User");
            var user = await AddUserAsync(
                "Anna",
                "Nowak",
                isBanned: true,
                bannedUntil: DateTime.UtcNow.AddDays(7),
                bannedReason: "Spam");

            // Act
            var result = await _sut.UnbanUserAsync(user.Id, admin.Id);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(result.Message).IsEqualTo("User unbanned successfully.");

            var updatedUser = await _context.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id);
            await Assert.That(updatedUser.IsBanned).IsFalse();
            await Assert.That(updatedUser.BannedUntil).IsNull();
            await Assert.That(updatedUser.BannedReason).IsNull();
        }

        [Test]
        public async Task UnbanUserAsync_WhenUserHasPermanentBan_ShouldClearBanFieldsAndLockout()
        {
            // Arrange
            var admin = await AddUserAsync("Admin", "User");
            var user = await AddUserAsync(
                "Anna",
                "Nowak",
                isBanned: true,
                bannedUntil: null,
                bannedReason: "Severe violation");

            // Act
            var result = await _sut.UnbanUserAsync(user.Id, admin.Id);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);

            var updatedUser = await _context.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id);
            await Assert.That(updatedUser.IsBanned).IsFalse();
            await Assert.That(updatedUser.BannedUntil).IsNull();
            await Assert.That(updatedUser.BannedReason).IsNull();
        }

        #endregion

        #region BanUserAsync

        [Test]
        public async Task BanUserAsync_WhenAdminTriesToBanThemselves_ShouldReturnBadRequestFailure()
        {
            // Arrange
            var admin = await AddUserAsync("Admin", "User");
            var command = new BanUserCommand
            {
                UserId = admin.Id,
                Reason = "Self ban test",
                Duration = TimeSpan.FromDays(1)
            };

            // Act
            var result = await _sut.BanUserAsync(command, admin.Id);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.InvalidOperation);
            await Assert.That(result.Message).IsEqualTo("You cannot ban yourself.");
        }

        [Test]
        public async Task BanUserAsync_WhenUserDoesNotExist_ShouldReturnNotFoundFailure()
        {
            // Arrange
            var admin = await AddUserAsync("Admin", "User");
            var command = new BanUserCommand
            {
                UserId = Guid.NewGuid(),
                Reason = "Spam",
                Duration = null
            };

            // Act
            var result = await _sut.BanUserAsync(command, admin.Id);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status404NotFound);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.NotFound);
        }

        [Test]
        public async Task BanUserAsync_WhenUserAlreadyHasActiveBan_ShouldReturnBadRequestFailure()
        {
            // Arrange
            var admin = await AddUserAsync("Admin", "User");
            var target = await AddUserAsync(
                "Anna",
                "Nowak",
                isBanned: true,
                bannedUntil: DateTime.UtcNow.AddDays(5),
                bannedReason: "Existing ban");

            var command = new BanUserCommand
            {
                UserId = target.Id,
                Reason = "Another reason",
                Duration = TimeSpan.FromDays(10)
            };

            // Act
            var result = await _sut.BanUserAsync(command, admin.Id);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.UserAlreadyBanned);
        }

        [Test]
        public async Task BanUserAsync_WithTemporaryDuration_ShouldBanUserAndApprovePendingReports()
        {
            // Arrange
            var admin = await AddUserAsync("Admin", "User");
            var reporter = await AddUserAsync("Adam", "Reporter");
            var target = await AddUserAsync("Anna", "Nowak");
            var pendingReport = await AddReportAsync(target, reporter, reason: "Pending spam report");

            var duration = TimeSpan.FromDays(3);
            var beforeBan = DateTime.UtcNow;
            var command = new BanUserCommand
            {
                UserId = target.Id,
                Reason = "Direct ban for spam",
                Duration = duration
            };

            // Act
            var result = await _sut.BanUserAsync(command, admin.Id);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(result.Message).IsEqualTo("User banned successfully.");

            var updatedUser = await _context.Users.AsNoTracking().SingleAsync(u => u.Id == target.Id);
            await Assert.That(updatedUser.IsBanned).IsTrue();
            await Assert.That(updatedUser.BannedReason).IsEqualTo("Direct ban for spam");
            await Assert.That(updatedUser.BannedUntil).IsNotNull();
            await Assert.That(updatedUser.BannedUntil!.Value).IsGreaterThanOrEqualTo(beforeBan.Add(duration));

            var updatedReport = await _context.UserReports.AsNoTracking().SingleAsync(r => r.Id == pendingReport.Id);
            await Assert.That(updatedReport.Status).IsEqualTo(ReportStatusEnum.Approved);
            await Assert.That(updatedReport.ResolvedByAdminId).IsEqualTo(admin.Id);
            await Assert.That(updatedReport.ResolvedAt).IsNotNull();
        }

        [Test]
        public async Task BanUserAsync_WithNullDuration_ShouldApplyPermanentBan()
        {
            // Arrange
            var admin = await AddUserAsync("Admin", "User");
            var target = await AddUserAsync("Anna", "Nowak");

            var command = new BanUserCommand
            {
                UserId = target.Id,
                Reason = "Permanent ban",
                Duration = null
            };

            // Act
            var result = await _sut.BanUserAsync(command, admin.Id);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();

            var updatedUser = await _context.Users.AsNoTracking().SingleAsync(u => u.Id == target.Id);
            await Assert.That(updatedUser.IsBanned).IsTrue();
            await Assert.That(updatedUser.BannedUntil).IsNull();
            await Assert.That(updatedUser.BannedReason).IsEqualTo("Permanent ban");
        }

        [Test]
        public async Task BanUserAsync_WithTemporaryDuration_ShouldSendTemporaryBanNotificationEmail()
        {
            // Arrange
            var admin = await AddUserAsync("Admin", "User");
            var target = await AddUserAsync("Anna", "Nowak");
            var duration = TimeSpan.FromDays(5);

            var command = new BanUserCommand
            {
                UserId = target.Id,
                Reason = "Spam links",
                Duration = duration
            };

            // Act
            var result = await _sut.BanUserAsync(command, admin.Id);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();

            var sentEmail = _emailService.SentEmails.SingleOrDefault(e => e.Type == FakeEmailService.AccountBannedNotificationType);
            await Assert.That(sentEmail).IsNotNull();
            await Assert.That(sentEmail!.To).IsEqualTo(target.Email);
            await Assert.That(sentEmail.Reason).IsEqualTo("Spam links");
            await Assert.That(sentEmail.BannedUntil).IsNotNull();
        }

        [Test]
        public async Task BanUserAsync_WhenEmailServiceThrows_ShouldStillBanUserSuccessfully()
        {
            // Arrange
            var admin = await AddUserAsync("Admin", "User");
            var target = await AddUserAsync("Anna", "Nowak");
            _emailService.ThrowOnSend = true;

            var command = new BanUserCommand
            {
                UserId = target.Id,
                Reason = "Direct ban despite SMTP failure",
                Duration = null
            };

            // Act
            var result = await _sut.BanUserAsync(command, admin.Id);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();

            var updatedUser = await _context.Users.AsNoTracking().SingleAsync(u => u.Id == target.Id);
            await Assert.That(updatedUser.IsBanned).IsTrue();
            await Assert.That(updatedUser.BannedReason).IsEqualTo("Direct ban despite SMTP failure");
        }

        [Test]
        public async Task BanUserAsync_WhenTargetUserIsAdmin_ShouldReturnBadRequestFailure()
        {
            // Arrange
            var admin = await AddAdminUserAsync("Admin", "One");
            var targetAdmin = await AddAdminUserAsync("Admin", "Two");

            var command = new BanUserCommand
            {
                UserId = targetAdmin.Id,
                Reason = "Attempting to ban another admin",
                Duration = TimeSpan.FromDays(7)
            };

            // Act
            var result = await _sut.BanUserAsync(command, admin.Id);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.InvalidOperation);
            await Assert.That(result.Message).IsEqualTo("You cannot ban an administrator.");

            var unchangedUser = await _context.Users.AsNoTracking().SingleAsync(u => u.Id == targetAdmin.Id);
            await Assert.That(unchangedUser.IsBanned).IsFalse();
        }
        #endregion
    }
}