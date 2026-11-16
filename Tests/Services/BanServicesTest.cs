using Domain.Const;
using Domain.Models;
using Infrastructure;
using Infrastructure.Interceptors;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using Services.Command.Ban;
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
        #endregion
    }
}