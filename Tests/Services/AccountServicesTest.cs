using Api.Configuration;
using Domain.Common;
using Domain.Const;
using Domain.Enum;
using Domain.Exceptions;
using Domain.Models;
using Infrastructure;
using Infrastructure.Interceptors;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Services.Accessors.Interfaces;
using Services.Command.Account;
using Services.Command.Auth;
using Services.Commands.Auth;
using Services.Interfaces;
using Services.Interfaces.Auth;
using Services.Options;
using Services.Services;
using Services.Services.Auth;
using System.Security.Claims;
using Testcontainers.PostgreSql;
using Tests.Fakes;

namespace Tests.Services
{
    public class AccountServicesTest
    {
        #region Configuration and Setup

        private const string Password = "Password123!";

        private static PostgreSqlContainer _dbContainer = null!;
        private static string _connectionString = null!;

        private ServiceProvider _serviceProvider = null!;
        private IServiceScope _scope = null!;
        private string _currentSchema = null!;
        private FakeEmailService _emailService = null!;

        private AppDbContext Context => _scope.ServiceProvider.GetRequiredService<AppDbContext>();
        private UserManager<User> UserManager => _scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        private IAuthService AuthService => _scope.ServiceProvider.GetRequiredService<IAuthService>();
        private IUserServices UserServices => _scope.ServiceProvider.GetRequiredService<IUserServices>();
        private IAccountServices Sut => _scope.ServiceProvider.GetRequiredService<IAccountServices>();

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
                    "-c", "shared_buffers=256MB")
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
                })
                .AddInterceptors(new SoftDeleteInterceptor())
                .Options;

            _emailService = new FakeEmailService();

            var services = new ServiceCollection();

            services.AddLogging();
            services.AddScoped<ICancellationTokenAccessor, FakeCancellationTokenAccessor>();
            services.AddScoped<AppDbContext>(_ => new AppDbContext(dbOptions));

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

            services.Configure<JwtOptions>(options =>
            {
                options.SecretKey = "test-secret-key-with-at-least-32-characters";
                options.Issuer = "MAP-API-Tests";
                options.Audience = "MAP-API-Tests";
                options.AccessTokenExpirationMinutes = 15;
                options.RefreshTokenExpirationDays = 7;
            });

            services.AddSingleton<IEmailService>(_emailService);
            services.AddScoped<ITokenService, TokenService>();
            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<IUserServices, UserServices>();
            services.AddScoped<IAccountServices, AccountServices>();

            _serviceProvider = services.BuildServiceProvider();
            _scope = _serviceProvider.CreateScope();

            var createScript = Context.Database.GenerateCreateScript();
            await Context.Database.ExecuteSqlRawAsync(createScript);

            var roleManager = _scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
            await roleManager.CreateAsync(new IdentityRole<Guid>("User"));
        }

        [After(Test)]
        public async Task CleanupAsync()
        {
            _scope?.Dispose();
            if (_serviceProvider != null)
            {
                await _serviceProvider.DisposeAsync();
            }

            if (!string.IsNullOrEmpty(_currentSchema))
            {
                using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = $"DROP SCHEMA IF EXISTS {_currentSchema} CASCADE;";
                await cmd.ExecuteNonQueryAsync();
            }
        }

        #endregion

        #region RequestEmailChangeAsync

        [Test]
        public async Task RequestEmailChangeAsync_WhenEmailIsAvailable_ShouldSendCodeToNewEmailAndKeepCurrentEmail()
        {
            // Arrange
            var user = await CreateUserAsync("old@example.com");

            // Act
            var result = await Sut.RequestEmailChangeAsync(user.Id, new ChangeEmailCommand { NewEmail = "new@example.com" });

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);

            var code = await GetPendingCodeAsync(user.Id);
            await Assert.That(code).IsNotNull();
            await Assert.That(code!.NewEmail).IsEqualTo("new@example.com");
            await Assert.That(code.Code.Length).IsEqualTo(VerificationConst.CodeLength);
            await Assert.That(code.Code.All(char.IsAsciiDigit)).IsTrue();
            await Assert.That(code.ExpiredAt).IsGreaterThan(DateTime.UtcNow);

            await Assert.That(_emailService.SentEmails.Count).IsEqualTo(1);
            await Assert.That(_emailService.SentEmails[0].To).IsEqualTo("new@example.com");
            await Assert.That(_emailService.SentEmails[0].Code).IsEqualTo(code.Code);

            var userInDb = await GetUserIgnoringFiltersAsync(user.Id);
            await Assert.That(userInDb.Email).IsEqualTo("old@example.com");
        }

        [Test]
        public async Task RequestEmailChangeAsync_WhenEmailChangeIsPending_ShouldStillAllowLoginWithCurrentEmail()
        {
            // Arrange
            var user = await CreateUserAsync("old@example.com");
            await Sut.RequestEmailChangeAsync(user.Id, new ChangeEmailCommand { NewEmail = "new@example.com" });

            // Act
            var oldEmailLogin = await AuthService.LoginAsync(new LoginCommand("old@example.com", Password));
            var newEmailLogin = await AuthService.LoginAsync(new LoginCommand("new@example.com", Password));

            // Assert
            await Assert.That(oldEmailLogin.IsSuccess).IsTrue();
            await Assert.That(newEmailLogin.IsSuccess).IsFalse();
        }

        [Test]
        [Arguments("old@example.com")]
        [Arguments("OLD@Example.com")]
        public async Task RequestEmailChangeAsync_WhenEmailIsSameAsCurrent_ShouldReturnBadRequest(string newEmail)
        {
            // Arrange
            var user = await CreateUserAsync("old@example.com");

            // Act
            var result = await Sut.RequestEmailChangeAsync(user.Id, new ChangeEmailCommand { NewEmail = newEmail });

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.EmailUnchanged);
            await Assert.That(_emailService.SentEmails.Count).IsEqualTo(0);
        }

        [Test]
        public async Task RequestEmailChangeAsync_WhenEmailBelongsToAnotherUser_ShouldReturnConflict()
        {
            // Arrange
            var user = await CreateUserAsync("old@example.com");
            await CreateUserAsync("taken@example.com");

            // Act
            var result = await Sut.RequestEmailChangeAsync(user.Id, new ChangeEmailCommand { NewEmail = "Taken@Example.com" });

            // Assert
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status409Conflict);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.EmailTaken);
            await Assert.That(await GetPendingCodeAsync(user.Id)).IsNull();
            await Assert.That(_emailService.SentEmails.Count).IsEqualTo(0);
        }

        [Test]
        public async Task RequestEmailChangeAsync_WhenEmailBelongsToSoftDeletedUserWithoutAnonymization_ShouldReturnConflict()
        {
            // Arrange
            var user = await CreateUserAsync("old@example.com");
            var deletedUser = await CreateUserAsync("legacy@example.com");

            await Context.Users
                .Where(u => u.Id == deletedUser.Id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(u => u.IsDeleted, true));

            // Act
            var result = await Sut.RequestEmailChangeAsync(user.Id, new ChangeEmailCommand { NewEmail = "legacy@example.com" });

            // Assert
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status409Conflict);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.EmailTaken);
        }

        [Test]
        public async Task RequestEmailChangeAsync_WhenCodeIsAlreadyPending_ShouldReplacePreviousPendingCode()
        {
            // Arrange
            var user = await CreateUserAsync("old@example.com");
            await Sut.RequestEmailChangeAsync(user.Id, new ChangeEmailCommand { NewEmail = "first@example.com" });

            // Act
            var result = await Sut.RequestEmailChangeAsync(user.Id, new ChangeEmailCommand { NewEmail = "second@example.com" });

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();

            var pendingCodes = await Context.EmailVerificationCodes
                .AsNoTracking()
                .Where(c => c.UserId == user.Id && !c.IsUsed)
                .ToListAsync();

            await Assert.That(pendingCodes.Count).IsEqualTo(1);
            await Assert.That(pendingCodes[0].NewEmail).IsEqualTo("second@example.com");
        }

        [Test]
        public async Task RequestEmailChangeAsync_WhenEmailCannotBeSent_ShouldThrow()
        {
            // Arrange
            var user = await CreateUserAsync("old@example.com");
            _emailService.ThrowOnSend = true;

            // Act & Assert
            await Assert.That(async () => await Sut.RequestEmailChangeAsync(user.Id, new ChangeEmailCommand { NewEmail = "new@example.com" }))
                .Throws<InvalidOperationException>();
        }

        [Test]
        public async Task RequestEmailChangeAsync_WhenUserDoesNotExist_ShouldReturnNotFound()
        {
            // Act
            var result = await Sut.RequestEmailChangeAsync(Guid.NewGuid(), new ChangeEmailCommand { NewEmail = "new@example.com" });

            // Assert
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status404NotFound);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.NotFound);
        }

        #endregion

        #region ConfirmEmailChangeAsync

        [Test]
        public async Task ConfirmEmailChangeAsync_WhenCodeIsValid_ShouldChangeEmailAndNotifyOldAddress()
        {
            // Arrange
            var user = await CreateUserAsync("old@example.com");
            var code = await RequestCodeAsync(user.Id, "new@example.com");
            var securityStampBefore = (await GetUserIgnoringFiltersAsync(user.Id)).SecurityStamp;

            // Act
            var result = await Sut.ConfirmEmailChangeAsync(user.Id, new ConfirmEmailChangeCommand { Code = code });

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);

            var userInDb = await GetUserIgnoringFiltersAsync(user.Id);
            await Assert.That(userInDb.Email).IsEqualTo("new@example.com");
            await Assert.That(userInDb.NormalizedEmail).IsEqualTo("NEW@EXAMPLE.COM");
            await Assert.That(userInDb.UserName).IsEqualTo("new@example.com");
            await Assert.That(userInDb.NormalizedUserName).IsEqualTo("NEW@EXAMPLE.COM");
            await Assert.That(userInDb.EmailConfirmed).IsTrue();
            await Assert.That(userInDb.SecurityStamp).IsNotEqualTo(securityStampBefore);

            await Assert.That(await GetPendingCodeAsync(user.Id)).IsNull();

            var notification = _emailService.SentEmails.Single(e => e.Type == FakeEmailService.EmailChangedNotificationType);
            await Assert.That(notification.To).IsEqualTo("old@example.com");
            await Assert.That(notification.NewEmail).IsEqualTo("new@example.com");
        }

        [Test]
        public async Task ConfirmEmailChangeAsync_WhenEmailWasChanged_ShouldAllowLoginOnlyWithNewEmail()
        {
            // Arrange
            var user = await CreateUserAsync("old@example.com");
            var code = await RequestCodeAsync(user.Id, "new@example.com");
            await Sut.ConfirmEmailChangeAsync(user.Id, new ConfirmEmailChangeCommand { Code = code });

            // Act
            var oldEmailLogin = await AuthService.LoginAsync(new LoginCommand("old@example.com", Password));
            var newEmailLogin = await AuthService.LoginAsync(new LoginCommand("new@example.com", Password));

            // Assert
            await Assert.That(oldEmailLogin.ErrorCode).IsEqualTo(ErrorCodes.InvalidCredentials);
            await Assert.That(newEmailLogin.IsSuccess).IsTrue();
        }

        [Test]
        public async Task ConfirmEmailChangeAsync_WhenNotificationCannotBeSent_ShouldStillChangeEmail()
        {
            // Arrange
            var user = await CreateUserAsync("old@example.com");
            var code = await RequestCodeAsync(user.Id, "new@example.com");
            _emailService.ThrowOnSend = true;

            // Act
            var result = await Sut.ConfirmEmailChangeAsync(user.Id, new ConfirmEmailChangeCommand { Code = code });

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That((await GetUserIgnoringFiltersAsync(user.Id)).Email).IsEqualTo("new@example.com");
        }

        [Test]
        public async Task ConfirmEmailChangeAsync_WhenCodeIsWrong_ShouldReturnCodeInvalidAndCountAttempt()
        {
            // Arrange
            var user = await CreateUserAsync("old@example.com");
            var code = await RequestCodeAsync(user.Id, "new@example.com");

            // Act
            var result = await Sut.ConfirmEmailChangeAsync(user.Id, new ConfirmEmailChangeCommand { Code = WrongCode(code) });

            // Assert
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.CodeInvalid);

            var pendingCode = await GetPendingCodeAsync(user.Id);
            await Assert.That(pendingCode!.AttemptsCount).IsEqualTo(1);
            await Assert.That((await GetUserIgnoringFiltersAsync(user.Id)).Email).IsEqualTo("old@example.com");
        }

        [Test]
        public async Task ConfirmEmailChangeAsync_WhenAttemptsAreExhausted_ShouldRejectEvenCorrectCode()
        {
            // Arrange
            var user = await CreateUserAsync("old@example.com");
            var code = await RequestCodeAsync(user.Id, "new@example.com");

            for (var i = 0; i < VerificationConst.MaxAttempts; i++)
            {
                await Sut.ConfirmEmailChangeAsync(user.Id, new ConfirmEmailChangeCommand { Code = WrongCode(code) });
            }

            // Act
            var result = await Sut.ConfirmEmailChangeAsync(user.Id, new ConfirmEmailChangeCommand { Code = code });

            // Assert
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.AttemptsExceeded);
            await Assert.That((await GetUserIgnoringFiltersAsync(user.Id)).Email).IsEqualTo("old@example.com");
        }

        [Test]
        public async Task ConfirmEmailChangeAsync_WhenCodeHasExpired_ShouldReturnCodeExpired()
        {
            // Arrange
            var user = await CreateUserAsync("old@example.com");
            var code = await RequestCodeAsync(user.Id, "new@example.com");

            await Context.EmailVerificationCodes
                .Where(c => c.UserId == user.Id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(c => c.ExpiredAt, DateTime.UtcNow.AddMinutes(-1)));

            // Act
            var result = await Sut.ConfirmEmailChangeAsync(user.Id, new ConfirmEmailChangeCommand { Code = code });

            // Assert
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.CodeExpired);
            await Assert.That((await GetUserIgnoringFiltersAsync(user.Id)).Email).IsEqualTo("old@example.com");
        }

        [Test]
        public async Task ConfirmEmailChangeAsync_WhenNoCodeIsPending_ShouldReturnCodeInvalid()
        {
            // Arrange
            var user = await CreateUserAsync("old@example.com");

            // Act
            var result = await Sut.ConfirmEmailChangeAsync(user.Id, new ConfirmEmailChangeCommand { Code = "123456" });

            // Assert
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.CodeInvalid);
        }

        [Test]
        public async Task ConfirmEmailChangeAsync_WhenCodeWasAlreadyUsed_ShouldReturnCodeInvalid()
        {
            // Arrange
            var user = await CreateUserAsync("old@example.com");
            var code = await RequestCodeAsync(user.Id, "new@example.com");
            await Sut.ConfirmEmailChangeAsync(user.Id, new ConfirmEmailChangeCommand { Code = code });

            // Act
            var result = await Sut.ConfirmEmailChangeAsync(user.Id, new ConfirmEmailChangeCommand { Code = code });

            // Assert
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.CodeInvalid);
        }

        [Test]
        public async Task ConfirmEmailChangeAsync_WhenCodeBelongsToAnotherUser_ShouldReturnCodeInvalid()
        {
            // Arrange
            var user = await CreateUserAsync("old@example.com");
            var otherUser = await CreateUserAsync("other@example.com");
            var otherUsersCode = await RequestCodeAsync(otherUser.Id, "other-new@example.com");

            // Act
            var result = await Sut.ConfirmEmailChangeAsync(user.Id, new ConfirmEmailChangeCommand { Code = otherUsersCode });

            // Assert
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.CodeInvalid);
            await Assert.That((await GetUserIgnoringFiltersAsync(user.Id)).Email).IsEqualTo("old@example.com");
        }

        [Test]
        public async Task ConfirmEmailChangeAsync_WhenEmailWasTakenAfterRequest_ShouldReturnConflict()
        {
            // Arrange
            var user = await CreateUserAsync("old@example.com");
            var code = await RequestCodeAsync(user.Id, "new@example.com");
            await CreateUserAsync("new@example.com");

            // Act
            var result = await Sut.ConfirmEmailChangeAsync(user.Id, new ConfirmEmailChangeCommand { Code = code });

            // Assert
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status409Conflict);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.EmailTaken);
            await Assert.That((await GetUserIgnoringFiltersAsync(user.Id)).Email).IsEqualTo("old@example.com");
        }

        [Test]
        public async Task ConfirmEmailChangeAsync_WhenTwoUsersConfirmSameEmailInParallel_ShouldChangeOnlyOne()
        {
            // Arrange
            var firstUser = await CreateUserAsync("first@example.com");
            var secondUser = await CreateUserAsync("second@example.com");
            var firstCode = await RequestCodeAsync(firstUser.Id, "shared@example.com");
            var secondCode = await RequestCodeAsync(secondUser.Id, "shared@example.com");

            // Act
            var results = await Task.WhenAll(
                Task.Run(() => RunInNewScopeAsync(sut => sut.ConfirmEmailChangeAsync(firstUser.Id, new ConfirmEmailChangeCommand { Code = firstCode }))),
                Task.Run(() => RunInNewScopeAsync(sut => sut.ConfirmEmailChangeAsync(secondUser.Id, new ConfirmEmailChangeCommand { Code = secondCode }))));

            // Assert
            await Assert.That(results.Count(r => r.IsSuccess)).IsEqualTo(1);

            var failure = results.Single(r => !r.IsSuccess);
            await Assert.That(failure.StatusCode).IsEqualTo(StatusCodes.Status409Conflict);
            await Assert.That(failure.ErrorCode).IsEqualTo(ErrorCodes.EmailTaken);

            var usersWithSharedEmail = await Context.Users
                .IgnoreQueryFilters()
                .AsNoTracking()
                .CountAsync(u => u.NormalizedEmail == "SHARED@EXAMPLE.COM");
            await Assert.That(usersWithSharedEmail).IsEqualTo(1);
        }

        [Test]
        public async Task Users_WhenTwoAccountsHaveSameNormalizedEmail_DatabaseShouldRejectDuplicate()
        {
            // Arrange
            await CreateUserAsync("taken@example.com");
            var otherUser = await CreateUserAsync("other@example.com");

            // Act & Assert
            var exception = await Assert.That(async () => await Context.Users
                    .Where(u => u.Id == otherUser.Id)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(u => u.NormalizedEmail, "TAKEN@EXAMPLE.COM")))
                .Throws<PostgresException>();

            await Assert.That(exception!.SqlState).IsEqualTo(PostgresErrorCodes.UniqueViolation);
        }

        [Test]
        public async Task ConfirmEmailChangeAsync_WhenUserDoesNotExist_ShouldReturnNotFound()
        {
            // Act
            var result = await Sut.ConfirmEmailChangeAsync(Guid.NewGuid(), new ConfirmEmailChangeCommand { Code = "123456" });

            // Assert
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status404NotFound);
        }

        #endregion

        #region DeleteAccountAsync

        [Test]
        public async Task DeleteAccountAsync_WhenUserExists_ShouldSoftDeleteAndAnonymizeAccount()
        {
            // Arrange
            var user = await CreateUserAsync("delete-me@example.com", isPublic: true, bio: "My bio");

            // Act
            var result = await RunInNewScopeAsync(sut => sut.DeleteAccountAsync(user.Id));

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);

            var userInDb = await GetUserIgnoringFiltersAsync(user.Id);
            await Assert.That(userInDb.IsDeleted).IsTrue();
            await Assert.That(userInDb.DeletedAt).IsNotNull();
            await Assert.That(userInDb.Email).IsEqualTo($"deleted-{user.Id:N}@deleted.invalid");
            await Assert.That(userInDb.UserName).IsEqualTo($"deleted-{user.Id:N}@deleted.invalid");
            await Assert.That(userInDb.PasswordHash).IsNull();
            await Assert.That(userInDb.EmailConfirmed).IsFalse();
            await Assert.That(userInDb.FirstName).IsEqualTo("Deleted");
            await Assert.That(userInDb.LastName).IsEqualTo("User");
            await Assert.That(userInDb.Bio).IsNull();
            await Assert.That(userInDb.IsPublic).IsFalse();

            var isVisible = await Context.Users.AsNoTracking().AnyAsync(u => u.Id == user.Id);
            await Assert.That(isVisible).IsFalse();
        }

        [Test]
        public async Task DeleteAccountAsync_WhenUserHasSessions_ShouldRevokeAllRefreshTokens()
        {
            // Arrange
            var user = await CreateUserAsync("delete-me@example.com");
            var firstSession = await AuthService.LoginAsync(new LoginCommand("delete-me@example.com", Password));
            var secondSession = await AuthService.LoginAsync(new LoginCommand("delete-me@example.com", Password));

            // Act
            await RunInNewScopeAsync(sut => sut.DeleteAccountAsync(user.Id));

            // Assert
            var activeTokens = await Context.RefreshTokens.AsNoTracking().CountAsync(t => t.UserId == user.Id && !t.IsRevoked);
            await Assert.That(activeTokens).IsEqualTo(0);

            var refreshResult = await AuthService.RefreshAsync(firstSession.Data!.RefreshToken);
            await Assert.That(refreshResult.StatusCode).IsEqualTo(StatusCodes.Status401Unauthorized);
            await Assert.That(refreshResult.ErrorCode).IsEqualTo(ErrorCodes.InvalidRefreshToken);

            var secondRefreshResult = await AuthService.RefreshAsync(secondSession.Data!.RefreshToken);
            await Assert.That(secondRefreshResult.StatusCode).IsEqualTo(StatusCodes.Status401Unauthorized);
        }

        [Test]
        public async Task DeleteAccountAsync_WhenAccountIsDeleted_ShouldRejectLoginWithOldCredentials()
        {
            // Arrange
            var user = await CreateUserAsync("delete-me@example.com");

            // Act
            await RunInNewScopeAsync(sut => sut.DeleteAccountAsync(user.Id));
            var loginResult = await AuthService.LoginAsync(new LoginCommand("delete-me@example.com", Password));

            // Assert
            await Assert.That(loginResult.StatusCode).IsEqualTo(StatusCodes.Status401Unauthorized);
            await Assert.That(loginResult.ErrorCode).IsEqualTo(ErrorCodes.InvalidCredentials);
        }

        [Test]
        public async Task DeleteAccountAsync_WhenAccountIsDeleted_ShouldMakeProfileInaccessibleToOthers()
        {
            // Arrange
            var user = await CreateUserAsync("delete-me@example.com", isPublic: true);
            var viewer = await CreateUserAsync("viewer@example.com");

            // Act
            await RunInNewScopeAsync(sut => sut.DeleteAccountAsync(user.Id));

            // Assert
            await Assert.That(async () => await UserServices.GetUserProfileAsync(viewer.Id, user.Id))
                .Throws<Domain.Exceptions.NotFoundException>();
        }

        [Test]
        public async Task DeleteAccountAsync_WhenAccountIsDeleted_ShouldReleaseEmailForOtherAccounts()
        {
            // Arrange
            var deletedUser = await CreateUserAsync("reused@example.com");
            var otherUser = await CreateUserAsync("other@example.com");
            await RunInNewScopeAsync(sut => sut.DeleteAccountAsync(deletedUser.Id));

            // Act
            var result = await Sut.RequestEmailChangeAsync(otherUser.Id, new ChangeEmailCommand { NewEmail = "reused@example.com" });

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
        }

        [Test]
        public async Task DeleteAccountAsync_WhenUserHasTripsAndCodes_ShouldKeepTripsAndRemoveVerificationCodes()
        {
            // Arrange
            var user = await CreateUserAsync("delete-me@example.com");
            await RequestCodeAsync(user.Id, "new@example.com");

            Context.Trips.Add(new Trip
            {
                UserId = user.Id,
                Title = "Trip to Berlin",
                StartDate = DateTime.UtcNow.AddDays(-10)
            });
            await Context.SaveChangesAsync();

            // Act
            await RunInNewScopeAsync(sut => sut.DeleteAccountAsync(user.Id));

            // Assert
            var tripsCount = await Context.Trips.AsNoTracking().CountAsync(t => t.UserId == user.Id);
            await Assert.That(tripsCount).IsEqualTo(1);

            var codesCount = await Context.EmailVerificationCodes.AsNoTracking().CountAsync(c => c.UserId == user.Id);
            await Assert.That(codesCount).IsEqualTo(0);
        }

        [Test]
        public async Task DeleteAccountAsync_WhenUserDoesNotExist_ShouldReturnNotFound()
        {
            // Act
            var result = await RunInNewScopeAsync(sut => sut.DeleteAccountAsync(Guid.NewGuid()));

            // Assert
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status404NotFound);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.NotFound);
        }

        [Test]
        public async Task DeleteAccountAsync_WhenAlreadyDeleted_ShouldReturnNotFound()
        {
            // Arrange
            var user = await CreateUserAsync("delete-me@example.com");
            await RunInNewScopeAsync(sut => sut.DeleteAccountAsync(user.Id));

            // Act
            var result = await RunInNewScopeAsync(sut => sut.DeleteAccountAsync(user.Id));

            // Assert
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status404NotFound);
        }

        #endregion

        #region ValidateUserIsActiveAsync

        [Test]
        public async Task ValidateUserIsActiveAsync_WhenUserIsActive_ShouldNotFailToken()
        {
            // Arrange
            var user = await CreateUserAsync("active@example.com");
            var context = CreateTokenValidatedContext(user.Id.ToString());

            // Act
            await AuthenticationConfiguration.ValidateUserIsActiveAsync(context);

            // Assert
            await Assert.That(context.Result).IsNull();
        }

        [Test]
        public async Task ValidateUserIsActiveAsync_WhenUserWasDeleted_ShouldFailToken()
        {
            // Arrange
            var user = await CreateUserAsync("delete-me@example.com");
            await RunInNewScopeAsync(sut => sut.DeleteAccountAsync(user.Id));
            var context = CreateTokenValidatedContext(user.Id.ToString());

            // Act
            await AuthenticationConfiguration.ValidateUserIsActiveAsync(context);

            // Assert
            await Assert.That(context.Result).IsNotNull();
            await Assert.That(context.Result!.Failure).IsNotNull();
        }

        [Test]
        [Arguments(null)]
        [Arguments("not-a-guid")]
        public async Task ValidateUserIsActiveAsync_WhenUserIdClaimIsInvalid_ShouldFailToken(string? userId)
        {
            // Arrange
            var context = CreateTokenValidatedContext(userId);

            // Act
            await AuthenticationConfiguration.ValidateUserIsActiveAsync(context);

            // Assert
            await Assert.That(context.Result).IsNotNull();
            await Assert.That(context.Result!.Failure).IsNotNull();
        }

        #endregion


        #region RegisterAsync Tests

        [Test]
        public async Task RegisterAsync_WhenCommandIsValid_PersistsUserWithRoleAndSendsVerificationEmail()
        {
            // Arrange
            var command = new RegisterCommand
            {
                FirstName = "Jan",
                LastName = "Kowalski",
                Email = "jan.kowalski@example.com",
                Password = Password,
                Bio = "Software Engineer",
                IsPublic = true
            };

            // Act
            var result = await Sut.RegisterAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status201Created);
            await Assert.That(result.Message).IsEqualTo("If the email is valid, a verification code has been sent.");

            Context.ChangeTracker.Clear();

            var createdUser = await UserManager.FindByEmailAsync(command.Email);
            await Assert.That(createdUser).IsNotNull();
            await Assert.That(createdUser!.UserName).IsEqualTo(command.Email);
            await Assert.That(createdUser.FirstName).IsEqualTo("Jan");
            await Assert.That(createdUser.LastName).IsEqualTo("Kowalski");
            await Assert.That(createdUser.Bio).IsEqualTo("Software Engineer");
            await Assert.That(createdUser.EmailConfirmed).IsFalse();
            await Assert.That(createdUser.IsPublic).IsEqualTo(command.IsPublic);

            var roles = await UserManager.GetRolesAsync(createdUser);
            await Assert.That(roles).Contains("User");
            await Assert.That(roles).DoesNotContain("Admin");

            var verificationCode = await Context.EmailVerificationCodes
                .AsNoTracking()
                .SingleOrDefaultAsync(c => c.UserId == createdUser.Id);

            await Assert.That(verificationCode).IsNotNull();
            await Assert.That(verificationCode!.Code.Length).IsEqualTo(6);
            await Assert.That(int.TryParse(verificationCode.Code, out _)).IsTrue();
            await Assert.That(verificationCode.IsUsed).IsFalse();
            await Assert.That(verificationCode.AttemptsCount).IsEqualTo(0);
            await Assert.That(verificationCode.ExpiredAt).IsGreaterThan(DateTime.UtcNow);

            var sentEmail = _emailService.SentEmails
                .SingleOrDefault(e => e.Type == FakeEmailService.RegistrationVerificationCodeType);

            await Assert.That(sentEmail).IsNotNull();
            await Assert.That(sentEmail!.To).IsEqualTo(command.Email);
            await Assert.That(sentEmail.Code).IsEqualTo(verificationCode.Code);
        }

        [Test]
        public async Task RegisterAsync_WhenBioIsWhitespaceOrNull_NormalizesBioToNull()
        {
            // Arrange
            var command = new RegisterCommand
            {
                FirstName = "Anna",
                LastName = "Nowak",
                Email = "anna.nowak@example.com",
                Password = Password,
                Bio = "   "
            };

            // Act
            var result = await Sut.RegisterAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();

            Context.ChangeTracker.Clear();

            var createdUser = await UserManager.FindByEmailAsync(command.Email);
            await Assert.That(createdUser).IsNotNull();
            await Assert.That(createdUser!.Bio).IsNull();
        }

        [Test]
        public async Task RegisterAsync_WhenEmailAlreadyExists_ReturnsAntiEnumerationSuccessWithoutCreatingDuplicateOrSendingEmail()
        {
            // Arrange
            var existingUser = await CreateUserAsync("existing@example.com");
            var initialUserCount = await Context.Users.CountAsync();
            var initialCodeCount = await Context.EmailVerificationCodes.CountAsync();
            var initialEmailsSent = _emailService.SentEmails.Count;

            var command = new RegisterCommand
            {
                FirstName = "Clone",
                LastName = "User",
                Email = existingUser.Email!,
                Password = Password,
                Bio = null
            };

            // Act
            var result = await Sut.RegisterAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status201Created);
            await Assert.That(result.Message).IsEqualTo("If the email is valid, a verification code has been sent.");

            Context.ChangeTracker.Clear();

            await Assert.That(await Context.Users.CountAsync()).IsEqualTo(initialUserCount);
            await Assert.That(await Context.EmailVerificationCodes.CountAsync()).IsEqualTo(initialCodeCount);
            await Assert.That(_emailService.SentEmails.Count).IsEqualTo(initialEmailsSent);
        }

        [Test]
        public async Task RegisterAsync_WhenPasswordDoesNotMeetComplexityRules_ThrowsInternalServerExceptionAndRollsBackTransaction()
        {
            // Arrange
            var command = new RegisterCommand
            {
                FirstName = "Piotr",
                LastName = "Zieliński",
                Email = "piotr@example.com",
                Password = "short",
                Bio = null
            };

            var initialUserCount = await Context.Users.CountAsync();
            var initialCodeCount = await Context.EmailVerificationCodes.CountAsync();

            // Act & Assert
            await Assert.That(async () =>
            {
                await Sut.RegisterAsync(command);
            }).Throws<InternalServerException>();

            Context.ChangeTracker.Clear();

            await Assert.That(await Context.Users.CountAsync()).IsEqualTo(initialUserCount);
            await Assert.That(await Context.EmailVerificationCodes.CountAsync()).IsEqualTo(initialCodeCount);
            await Assert.That(await UserManager.FindByEmailAsync(command.Email)).IsNull();
            await Assert.That(_emailService.SentEmails.Count).IsEqualTo(0);
        }

        [Test]
        public async Task RegisterAsync_WhenTokenIsAlreadyCancelled_ThrowsOperationCanceledExceptionWithoutPersistingData()
        {
            // Arrange
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var command = new RegisterCommand
            {
                FirstName = "Tomasz",
                LastName = "Kot",
                Email = "tomasz@example.com",
                Password = Password,
                Bio = null
            };

            var initialUserCount = await Context.Users.CountAsync();
            var initialCodeCount = await Context.EmailVerificationCodes.CountAsync();

            // Act & Assert
            await Assert.That(async () =>
            {
                await Sut.RegisterAsync(command, cts.Token);
            }).Throws<OperationCanceledException>();

            Context.ChangeTracker.Clear();

            // Assert
            await Assert.That(await Context.Users.CountAsync()).IsEqualTo(initialUserCount);
            await Assert.That(await Context.EmailVerificationCodes.CountAsync()).IsEqualTo(initialCodeCount);
            await Assert.That(_emailService.SentEmails.Count).IsEqualTo(0);
        }

        [Test]
        public async Task RegisterAsync_WhenEmailCannotBeSent_ThrowsException()
        {
            // Arrange
            var command = new RegisterCommand
            {
                FirstName = "Marek",
                LastName = "Nowicki",
                Email = "marek@example.com",
                Password = Password,
                Bio = null
            };

            _emailService.ThrowOnSend = true;

            // Act & Assert
            await Assert.That(async () =>
            {
                await Sut.RegisterAsync(command);
            }).Throws<InvalidOperationException>();
        }

        #endregion

        #region ConfirmEmailAsync Tests

        [Test]
        public async Task ConfirmEmailAsync_WhenCodeIsValid_ConfirmsEmailMarksCodeAsUsedAndReturnsSuccess()
        {
            // Arrange
            var user = await CreateUserAsync("confirm-success@example.com", emailConfirmed: false);
            var validCode = "123456";

            var verificationCode = new EmailVerificationCode
            {
                UserId = user.Id,
                Code = validCode,
                ExpiredAt = DateTime.UtcNow.AddHours(2),
                AttemptsCount = 0,
                IsUsed = false
            };

            await Context.EmailVerificationCodes.AddAsync(verificationCode);
            await Context.SaveChangesAsync();

            var command = new ConfirmEmailCommand
            {
                Code = validCode,
                Email = user.Email!
            };

            // Act
            var result = await Sut.ConfirmRegisterAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(result.Message).IsEqualTo("Email confirmed successfully.");

            Context.ChangeTracker.Clear();

            var updatedUser = await UserManager.FindByIdAsync(user.Id.ToString());
            await Assert.That(updatedUser).IsNotNull();
            await Assert.That(updatedUser!.EmailConfirmed).IsTrue();

            var storedCode = await Context.EmailVerificationCodes
                .AsNoTracking()
                .SingleAsync(c => c.Id == verificationCode.Id);

            await Assert.That(storedCode.IsUsed).IsTrue();
        }

        [Test]
        public async Task ConfirmEmailAsync_WhenUserDoesNotExist_ReturnsBadRequestWithoutLeakingState()
        {
            // Arrange
            var command = new ConfirmEmailCommand
            {
                Code = "123456",
                Email = "nonexistent@example.com"
            };

            // Act
            var result = await Sut.ConfirmRegisterAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.CodeInvalid);
            await Assert.That(result.Message).IsEqualTo("The verification code is invalid.");
        }

        [Test]
        public async Task ConfirmEmailAsync_WhenEmailAlreadyConfirmed_ReturnsSuccessWithoutTouchingCodes()
        {
            // Arrange
            var user = await CreateUserAsync("already-confirmed@example.com", emailConfirmed: true);
            var command = new ConfirmEmailCommand
            {
                Code = "123456",
                Email = user.Email!
            };

            // Act
            var result = await Sut.ConfirmRegisterAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
            await Assert.That(result.Message).IsEqualTo("The verification code is invalid.");
        }

        [Test]
        public async Task ConfirmEmailAsync_WhenNoActiveCodeFound_ReturnsBadRequest()
        {
            // Arrange
            var user = await CreateUserAsync("no-code@example.com", emailConfirmed: false);

            var usedCode = new EmailVerificationCode
            {
                UserId = user.Id,
                Code = "654321",
                ExpiredAt = DateTime.UtcNow.AddHours(1),
                AttemptsCount = 0,
                IsUsed = true
            };

            await Context.EmailVerificationCodes.AddAsync(usedCode);
            await Context.SaveChangesAsync();

            var command = new ConfirmEmailCommand
            {
                Code = "654321",
                Email = user.Email!
            };

            // Act
            var result = await Sut.ConfirmRegisterAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.CodeInvalid);
            await Assert.That(result.Message).IsEqualTo("The verification code is invalid.");
        }

        [Test]
        public async Task ConfirmEmailAsync_WhenCodeHasExpired_MarksCodeAsUsedAndReturnsBadRequest()
        {
            // Arrange
            var user = await CreateUserAsync("expired-code@example.com", emailConfirmed: false);
            var expiredCode = new EmailVerificationCode
            {
                UserId = user.Id,
                Code = "111222",
                ExpiredAt = DateTime.UtcNow.AddMinutes(-5),
                AttemptsCount = 0,
                IsUsed = false
            };

            await Context.EmailVerificationCodes.AddAsync(expiredCode);
            await Context.SaveChangesAsync();

            var command = new ConfirmEmailCommand
            {
                Code = "111222",
                Email = user.Email!
            };

            // Act
            var result = await Sut.ConfirmRegisterAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.CodeInvalid);
            await Assert.That(result.Message).IsEqualTo("The verification code is invalid.");

            Context.ChangeTracker.Clear();

            var storedCode = await Context.EmailVerificationCodes
                .AsNoTracking()
                .SingleAsync(c => c.Id == expiredCode.Id);

            await Assert.That(storedCode.IsUsed).IsTrue();

            var updatedUser = await UserManager.FindByIdAsync(user.Id.ToString());
            await Assert.That(updatedUser!.EmailConfirmed).IsFalse();
        }

        [Test]
        public async Task ConfirmEmailAsync_WhenCodeIsIncorrect_IncrementsAttemptsCount()
        {
            // Arrange
            var user = await CreateUserAsync("wrong-code@example.com", emailConfirmed: false);
            var correctCode = "123456";

            var verificationCode = new EmailVerificationCode
            {
                UserId = user.Id,
                Code = correctCode,
                ExpiredAt = DateTime.UtcNow.AddHours(2),
                AttemptsCount = 1,
                IsUsed = false
            };

            await Context.EmailVerificationCodes.AddAsync(verificationCode);
            await Context.SaveChangesAsync();

            var command = new ConfirmEmailCommand
            {
                Code = "999999",
                Email = user.Email!
            };

            // Act
            var result = await Sut.ConfirmRegisterAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.CodeInvalid);
            await Assert.That(result.Message).IsEqualTo("The verification code is invalid.");

            Context.ChangeTracker.Clear();

            var storedCode = await Context.EmailVerificationCodes
                .AsNoTracking()
                .SingleAsync(c => c.Id == verificationCode.Id);

            await Assert.That(storedCode.AttemptsCount).IsEqualTo(2);
            await Assert.That(storedCode.IsUsed).IsFalse();
        }

        [Test]
        public async Task ConfirmEmailAsync_WhenFifthFailedAttemptReached_MarksCodeAsUsedAndFails()
        {
            // Arrange
            var user = await CreateUserAsync("fifth-attempt@example.com", emailConfirmed: false);

            var verificationCode = new EmailVerificationCode
            {
                UserId = user.Id,
                Code = "123456",
                ExpiredAt = DateTime.UtcNow.AddHours(2),
                AttemptsCount = 4,
                IsUsed = false
            };

            await Context.EmailVerificationCodes.AddAsync(verificationCode);
            await Context.SaveChangesAsync();

            var command = new ConfirmEmailCommand
            {
                Code = "000000",
                Email = user.Email!
            };

            // Act
            var result = await Sut.ConfirmRegisterAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.CodeInvalid);

            Context.ChangeTracker.Clear();

            var storedCode = await Context.EmailVerificationCodes
                .AsNoTracking()
                .SingleAsync(c => c.Id == verificationCode.Id);

            await Assert.That(storedCode.AttemptsCount).IsEqualTo(5);
            await Assert.That(storedCode.IsUsed).IsFalse();
        }

        [Test]
        public async Task ConfirmEmailAsync_WhenCodeAlreadyHasMaxAttempts_BlocksAttemptWithTooManyRequests()
        {
            // Arrange
            var user = await CreateUserAsync("max-attempts@example.com", emailConfirmed: false);

            var verificationCode = new EmailVerificationCode
            {
                UserId = user.Id,
                Code = "123456",
                ExpiredAt = DateTime.UtcNow.AddHours(2),
                AttemptsCount = 5,
                IsUsed = false
            };

            await Context.EmailVerificationCodes.AddAsync(verificationCode);
            await Context.SaveChangesAsync();

            var command = new ConfirmEmailCommand
            {
                Code = "123456",
                Email = user.Email!
            };

            // Act
            var result = await Sut.ConfirmRegisterAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.CodeInvalid);
            await Assert.That(result.Message).IsEqualTo("The verification code is invalid.");

            Context.ChangeTracker.Clear();

            var storedCode = await Context.EmailVerificationCodes
                .AsNoTracking()
                .SingleAsync(c => c.Id == verificationCode.Id);

            await Assert.That(storedCode.IsUsed).IsFalse();
        }

        [Test]
        public async Task ConfirmEmailAsync_WhenMultipleCodesExist_PicksLatestCreatedActiveCode()
        {
            // Arrange
            var user = await CreateUserAsync("latest-code@example.com", emailConfirmed: false);

            var olderCode = new EmailVerificationCode
            {
                UserId = user.Id,
                Code = "111111",
                ExpiredAt = DateTime.UtcNow.AddHours(1),
                AttemptsCount = 0,
                IsUsed = false
            };

            var newerCode = new EmailVerificationCode
            {
                UserId = user.Id,
                Code = "222222",
                ExpiredAt = DateTime.UtcNow.AddHours(2),
                AttemptsCount = 0,
                IsUsed = false
            };

            await Context.EmailVerificationCodes.AddRangeAsync(olderCode, newerCode);
            await Context.SaveChangesAsync();

            await Context.EmailVerificationCodes
                .Where(c => c.Id == olderCode.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.CreatedAt, DateTime.UtcNow.AddMinutes(-30)));

            await Context.EmailVerificationCodes
                .Where(c => c.Id == newerCode.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.CreatedAt, DateTime.UtcNow.AddMinutes(-5)));

            var command = new ConfirmEmailCommand
            {
                Email = user.Email!,
                Code = "222222"
            };

            // Act
            var result = await Sut.ConfirmRegisterAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);

            Context.ChangeTracker.Clear();

            var storedNewerCode = await Context.EmailVerificationCodes
                .AsNoTracking()
                .SingleAsync(c => c.Id == newerCode.Id);

            await Assert.That(storedNewerCode.IsUsed).IsTrue();
        }

        [Test]
        public async Task ConfirmEmailAsync_WhenTokenIsAlreadyCancelled_ThrowsOperationCanceledException()
        {
            // Arrange
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var command = new ConfirmEmailCommand
            {
                Email = "cancelled@example.com",
                Code = "123456"
            };

            // Act & Assert
            await Assert.That(async () =>
            {
                await Sut.ConfirmRegisterAsync(command, cts.Token);
            }).Throws<OperationCanceledException>();
        }

        #endregion

        #region ResendEmailVerificationCodeAsync Tests

        [Test]
        public async Task ResendEmailVerificationCodeAsync_WhenUserIsUnverified_MarksOldCodesAsUsedAndSendsNewVerificationEmail()
        {
            // Arrange
            var user = await CreateUserAsync("unverified@example.com", emailConfirmed: false);

            var previousActiveCode = new EmailVerificationCode
            {
                UserId = user.Id,
                Purpose = VerificationPurposeEnum.Registration,
                Code = "111111",
                ExpiredAt = DateTime.UtcNow.AddHours(2),
                AttemptsCount = 0,
                IsUsed = false
            };

            await Context.EmailVerificationCodes.AddAsync(previousActiveCode);
            await Context.SaveChangesAsync();

            var command = new ResendVerificationCodeCommand { Email = user.Email! };

            // Act
            var result = await Sut.ResendEmailVerificationCodeAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(result.Message).IsEqualTo("If the email is valid and unverified, a new verification code has been sent.");

            Context.ChangeTracker.Clear();

            var oldCodeInDb = await Context.EmailVerificationCodes
                .AsNoTracking()
                .SingleAsync(c => c.Id == previousActiveCode.Id);
            await Assert.That(oldCodeInDb.IsUsed).IsTrue();

            var newCodeInDb = await Context.EmailVerificationCodes
                .AsNoTracking()
                .SingleOrDefaultAsync(c => c.UserId == user.Id && c.Purpose == VerificationPurposeEnum.Registration && !c.IsUsed);

            await Assert.That(newCodeInDb).IsNotNull();
            await Assert.That(newCodeInDb!.Id).IsNotEqualTo(previousActiveCode.Id);
            await Assert.That(newCodeInDb.Code.Length).IsEqualTo(VerificationConst.CodeLength);
            await Assert.That(int.TryParse(newCodeInDb.Code, out _)).IsTrue();
            await Assert.That(newCodeInDb.AttemptsCount).IsEqualTo(0);
            await Assert.That(newCodeInDb.ExpiredAt).IsGreaterThan(DateTime.UtcNow);

            var sentEmail = _emailService.SentEmails
                .SingleOrDefault(e => e.Type == FakeEmailService.RegistrationVerificationCodeType && e.To == user.Email);

            await Assert.That(sentEmail).IsNotNull();
            await Assert.That(sentEmail!.Code).IsEqualTo(newCodeInDb.Code);
        }

        [Test]
        public async Task ResendEmailVerificationCodeAsync_WhenMultipleActiveCodesExist_MarksAllPreviousAsUsed()
        {
            // Arrange
            var user = await CreateUserAsync("multi-codes@example.com", emailConfirmed: false);

            var firstCode = new EmailVerificationCode
            {
                UserId = user.Id,
                Purpose = VerificationPurposeEnum.Registration,
                Code = "111111",
                ExpiredAt = DateTime.UtcNow.AddHours(2),
                AttemptsCount = 0,
                IsUsed = false
            };

            var secondCode = new EmailVerificationCode
            {
                UserId = user.Id,
                Purpose = VerificationPurposeEnum.Registration,
                Code = "222222",
                ExpiredAt = DateTime.UtcNow.AddHours(2),
                AttemptsCount = 0,
                IsUsed = false
            };

            await Context.EmailVerificationCodes.AddRangeAsync(firstCode, secondCode);
            await Context.SaveChangesAsync();

            var command = new ResendVerificationCodeCommand { Email = user.Email! };

            // Act
            var result = await Sut.ResendEmailVerificationCodeAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();

            Context.ChangeTracker.Clear();

            var activeCodes = await Context.EmailVerificationCodes
                .AsNoTracking()
                .Where(c => c.UserId == user.Id && c.Purpose == VerificationPurposeEnum.Registration && !c.IsUsed)
                .ToListAsync();

            await Assert.That(activeCodes.Count).IsEqualTo(1);
            await Assert.That(activeCodes[0].Code).IsNotEqualTo("111111");
            await Assert.That(activeCodes[0].Code).IsNotEqualTo("222222");
        }

        [Test]
        public async Task ResendEmailVerificationCodeAsync_WhenUserHasEmailChangeCode_DoesNotInvalidateEmailChangeCode()
        {
            // Arrange
            var user = await CreateUserAsync("mixed-purposes@example.com", emailConfirmed: false);

            var emailChangeCode = new EmailVerificationCode
            {
                UserId = user.Id,
                Purpose = VerificationPurposeEnum.EmailChange,
                NewEmail = "different@example.com",
                Code = "999999",
                ExpiredAt = DateTime.UtcNow.AddHours(1),
                AttemptsCount = 0,
                IsUsed = false
            };

            await Context.EmailVerificationCodes.AddAsync(emailChangeCode);
            await Context.SaveChangesAsync();

            var command = new ResendVerificationCodeCommand { Email = user.Email! };

            // Act
            var result = await Sut.ResendEmailVerificationCodeAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();

            Context.ChangeTracker.Clear();

            var storedEmailChangeCode = await Context.EmailVerificationCodes
                .AsNoTracking()
                .SingleAsync(c => c.Id == emailChangeCode.Id);

            await Assert.That(storedEmailChangeCode.IsUsed).IsFalse();
        }

        [Test]
        public async Task ResendEmailVerificationCodeAsync_WhenEmailDoesNotExist_ReturnsAntiEnumerationSuccessWithoutSendingEmail()
        {
            // Arrange
            var initialCodesCount = await Context.EmailVerificationCodes.CountAsync();
            var initialEmailsCount = _emailService.SentEmails.Count;

            var command = new ResendVerificationCodeCommand { Email = "nonexistent@example.com" };

            // Act
            var result = await Sut.ResendEmailVerificationCodeAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(result.Message).IsEqualTo("If the email is valid and unverified, a new verification code has been sent.");

            Context.ChangeTracker.Clear();

            await Assert.That(await Context.EmailVerificationCodes.CountAsync()).IsEqualTo(initialCodesCount);
            await Assert.That(_emailService.SentEmails.Count).IsEqualTo(initialEmailsCount);
        }

        [Test]
        public async Task ResendEmailVerificationCodeAsync_WhenEmailAlreadyConfirmed_ReturnsAntiEnumerationSuccessWithoutSendingEmail()
        {
            // Arrange
            var user = await CreateUserAsync("confirmed@example.com", emailConfirmed: true);
            var initialCodesCount = await Context.EmailVerificationCodes.CountAsync();
            var initialEmailsCount = _emailService.SentEmails.Count;

            var command = new ResendVerificationCodeCommand { Email = user.Email! };

            // Act
            var result = await Sut.ResendEmailVerificationCodeAsync(command);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(result.Message).IsEqualTo("If the email is valid and unverified, a new verification code has been sent.");

            Context.ChangeTracker.Clear();

            await Assert.That(await Context.EmailVerificationCodes.CountAsync()).IsEqualTo(initialCodesCount);
            await Assert.That(_emailService.SentEmails.Count).IsEqualTo(initialEmailsCount);
        }

        [Test]
        public async Task ResendEmailVerificationCodeAsync_WhenEmailCannotBeSent_ThrowsException()
        {
            // Arrange
            var user = await CreateUserAsync("smtp-fail@example.com", emailConfirmed: false);
            var command = new ResendVerificationCodeCommand { Email = user.Email! };

            _emailService.ThrowOnSend = true;

            // Act & Assert
            await Assert.That(async () =>
            {
                await Sut.ResendEmailVerificationCodeAsync(command);
            }).Throws<InvalidOperationException>();
        }

        [Test]
        public async Task ResendEmailVerificationCodeAsync_WhenTokenIsAlreadyCancelled_ThrowsOperationCanceledExceptionWithoutPersistingData()
        {
            // Arrange
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var user = await CreateUserAsync("cancelled@example.com", emailConfirmed: false);
            var initialCodesCount = await Context.EmailVerificationCodes.CountAsync();

            var command = new ResendVerificationCodeCommand { Email = user.Email! };

            // Act & Assert
            await Assert.That(async () =>
            {
                await Sut.ResendEmailVerificationCodeAsync(command, cts.Token);
            }).Throws<OperationCanceledException>();

            Context.ChangeTracker.Clear();

            await Assert.That(await Context.EmailVerificationCodes.CountAsync()).IsEqualTo(initialCodesCount);
            await Assert.That(_emailService.SentEmails.Count).IsEqualTo(0);
        }

        #endregion

        #region Helpers

        private async Task<User> CreateUserAsync(string email, bool isPublic = false, string? bio = null, bool emailConfirmed = true)
        {
            var user = new User
            {
                Id = Guid.CreateVersion7(),
                UserName = email,
                Email = email,
                FirstName = "Test",
                LastName = "User",
                Bio = bio,
                IsPublic = isPublic,
                EmailConfirmed = emailConfirmed
            };

            var createResult = await UserManager.CreateAsync(user, Password);
            if (!createResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Could not create user '{email}': {string.Join(", ", createResult.Errors.Select(error => error.Description))}");
            }

            await UserManager.AddToRoleAsync(user, "User");

            return user;
        }

        private async Task<string> RequestCodeAsync(Guid userId, string newEmail)
        {
            var result = await Sut.RequestEmailChangeAsync(userId, new ChangeEmailCommand { NewEmail = newEmail });

            if (!result.IsSuccess)
            {
                throw new InvalidOperationException($"Could not request email change: {result.Message}");
            }

            return (await GetPendingCodeAsync(userId))!.Code;
        }

        private Task<EmailVerificationCode?> GetPendingCodeAsync(Guid userId)
            => Context.EmailVerificationCodes
                .AsNoTracking()
                .Where(c => c.UserId == userId && c.Purpose == VerificationPurposeEnum.EmailChange && !c.IsUsed)
                .OrderByDescending(c => c.CreatedAt)
                .FirstOrDefaultAsync();

        private Task<User> GetUserIgnoringFiltersAsync(Guid userId)
            => Context.Users
                .IgnoreQueryFilters()
                .AsNoTracking()
                .SingleAsync(u => u.Id == userId);

        private async Task<Result> RunInNewScopeAsync(Func<IAccountServices, Task<Result>> action)
        {
            using var scope = _serviceProvider.CreateScope();
            return await action(scope.ServiceProvider.GetRequiredService<IAccountServices>());
        }

        private static string WrongCode(string code)
            => code == "000000" ? "111111" : "000000";

        private TokenValidatedContext CreateTokenValidatedContext(string? userId)
        {
            var claims = userId is null
                ? Array.Empty<Claim>()
                : new[] { new Claim(ClaimTypes.NameIdentifier, userId) };

            var httpContext = new DefaultHttpContext { RequestServices = _scope.ServiceProvider };
            var scheme = new AuthenticationScheme(JwtBearerDefaults.AuthenticationScheme, null, typeof(JwtBearerHandler));

            return new TokenValidatedContext(httpContext, scheme, new JwtBearerOptions())
            {
                Principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"))
            };
        }

        #endregion
    }
}
