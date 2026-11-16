using Domain.Const;
using Domain.Enum;
using Domain.Models;
using Infrastructure;
using Infrastructure.Interceptors;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using Services.Command.Ban;
using Services.Command.List;
using Services.Interfaces;
using Services.Services;
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

            _sut = new BanServices(
                _context,
                new LoggerFactory().CreateLogger<BanServices>(),
                new FakeCancellationTokenAccessor());
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

        private async Task<User> AddUserAsync(string firstName = "Jan", string lastName = "Kowalski")
        {
            var user = new User
            {
                Id = Guid.NewGuid(),
                FirstName = firstName,
                LastName = lastName
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return user;
        }

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
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
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
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.UserNotFound);
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
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.UserNotFound);
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
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.UserNotFound);
            await Assert.That(await _context.UserReports.AnyAsync()).IsFalse();
        }

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
            await Assert.That(item.FirstName).IsEqualTo("Anna");
            await Assert.That(item.LastName).IsEqualTo("Nowak");
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
    }
}