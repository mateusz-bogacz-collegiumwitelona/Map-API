using Domain.Const;
using Domain.Models;
using Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Services.Accessors;
using Services.Accessors.Interfaces;
using Services.Commands.Auth;
using Services.Interfaces.Auth;
using Services.Options;
using Services.Services.Auth;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Tests.Auth
{
    public class AuthServiceTests
    {
        #region Shared Test Infrastructure

        private const string Password = "Password123!";
        private const int MaxFailedAccessAttempts = 5;
        private const string JwtSecretKey = "test-secret-key-with-at-least-32-characters";
        private const string JwtIssuer = "MAP-API-Tests";
        private const string JwtAudience = "MAP-API-Tests";

        private ServiceProvider _serviceProvider = null!;
        private IServiceScope _scope = null!;
        private SqliteConnection _connection = null!;
        private JwtOptions _jwtOptions = null!;

        private AppDbContext Context => _scope.ServiceProvider.GetRequiredService<AppDbContext>();
        private UserManager<User> UserManager => _scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        private RoleManager<IdentityRole<Guid>> RoleManager => _scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        private CountingPasswordHasher CountingHasher => _scope.ServiceProvider.GetRequiredService<CountingPasswordHasher>();
        private IAuthService Sut => _scope.ServiceProvider.GetRequiredService<IAuthService>();

        [Before(Test)]
        public async Task SetupAsync()
        {
            _jwtOptions = new JwtOptions
            {
                SecretKey = JwtSecretKey,
                Issuer = JwtIssuer,
                Audience = JwtAudience,
                AccessTokenExpirationMinutes = 15,
                RefreshTokenExpirationDays = 7
            };

            var services = new ServiceCollection();

            _connection = new SqliteConnection("DataSource=:memory:");
            await _connection.OpenAsync();

            services.AddLogging();
            services.AddHttpContextAccessor();
            services.AddScoped<ICancellationTokenAccessor, HttpCancellationTokenAccessor>();

            services.AddScoped<AuthTestDbContext>(_ =>
                new AuthTestDbContext(
                    new DbContextOptionsBuilder<AppDbContext>()
                        .UseSqlite(_connection)
                        .Options));
            services.AddScoped<AppDbContext>(provider => provider.GetRequiredService<AuthTestDbContext>());

            services.AddIdentity<User, IdentityRole<Guid>>(options =>
            {
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequiredLength = 8;
                options.Password.RequiredUniqueChars = 1;

                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = MaxFailedAccessAttempts;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddEntityFrameworkStores<AuthTestDbContext>()
            .AddDefaultTokenProviders();

            services.AddScoped<CountingPasswordHasher>();
            services.AddScoped<IPasswordHasher<User>>(provider => provider.GetRequiredService<CountingPasswordHasher>());

            services.Configure<JwtOptions>(options =>
            {
                options.SecretKey = _jwtOptions.SecretKey;
                options.Issuer = _jwtOptions.Issuer;
                options.Audience = _jwtOptions.Audience;
                options.AccessTokenExpirationMinutes = _jwtOptions.AccessTokenExpirationMinutes;
                options.RefreshTokenExpirationDays = _jwtOptions.RefreshTokenExpirationDays;
            });

            services.AddScoped<ITokenService, TokenService>();
            services.AddScoped<IAuthService, AuthService>();

            _serviceProvider = services.BuildServiceProvider();
            _scope = _serviceProvider.CreateScope();

            await Context.Database.EnsureCreatedAsync();

            await CreateRoleAsync("User");
            await CreateRoleAsync("Admin");
        }

        [After(Test)]
        public async Task TearDownAsync()
        {
            _scope.Dispose();
            await _serviceProvider.DisposeAsync();
            await _connection.DisposeAsync();
        }

        #endregion

        #region Constructor Tests

        [Test]
        public async Task Constructor_ComputesDummyPasswordHashWithInjectedPasswordHasher()
        {
            // Act
            _ = Sut;

            // Assert
            await Assert.That(CountingHasher.DummyHashCount).IsEqualTo(1);
        }

        #endregion

        #region LoginAsync Tests

        [Test]
        public async Task LoginAsync_WhenCredentialsAreValid_ReturnsAuthResponseWithClaimsAndPersistedRefreshToken()
        {
            // Arrange
            var user = await CreateUserAsync("confirmed@example.com");

            // Act
            var result = await Sut.LoginAsync(new LoginCommand(user.Email!, Password));

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(result.Data).IsNotNull();

            var data = result.Data!;

            await Assert.That(data.UserId).IsEqualTo(user.Id);
            await Assert.That(data.Roles).Contains("User");
            await Assert.That(string.IsNullOrWhiteSpace(data.AccessToken)).IsFalse();
            await Assert.That(data.AccessTokenExpiration).IsGreaterThan(DateTime.UtcNow);

            var principal = ValidateAccessToken(data.AccessToken);

            var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)
                ?? principal.FindFirst(JwtRegisteredClaimNames.Sub);
            var emailClaim = principal.FindFirst(ClaimTypes.Email)
                ?? principal.FindFirst(JwtRegisteredClaimNames.Email);

            await Assert.That(userIdClaim).IsNotNull();
            await Assert.That(userIdClaim!.Value).IsEqualTo(user.Id.ToString());
            await Assert.That(emailClaim).IsNotNull();
            await Assert.That(emailClaim!.Value).IsEqualTo(user.Email);

            var hasUserRole = principal.FindAll(ClaimTypes.Role).Any(claim => claim.Value == "User")
                || principal.FindAll("role").Any(claim => claim.Value == "User");
            await Assert.That(hasUserRole).IsTrue();

            var storedToken = await Context.RefreshTokens
                .AsNoTracking()
                .SingleAsync(token => token.Token == data.RefreshToken);

            await Assert.That(storedToken.UserId).IsEqualTo(user.Id);
            await Assert.That(storedToken.IsRevoked).IsFalse();
            await Assert.That(storedToken.RevokedAt).IsNull();
            await Assert.That(storedToken.ExpiredAt).IsGreaterThan(DateTime.UtcNow);
        }

        [Test]
        public async Task LoginAsync_WhenCredentialsAreInvalid_ReturnsIdenticalGenericErrorWithoutLeakingAccountExistence()
        {
            // Arrange
            var user = await CreateUserAsync("known@example.com");

            // Act
            var unknownEmailResult = await Sut.LoginAsync(new LoginCommand("unknown@example.com", Password));
            var wrongPasswordResult = await Sut.LoginAsync(new LoginCommand(user.Email!, "WrongPassword123!"));

            // Assert
            await Assert.That(unknownEmailResult.IsSuccess).IsFalse();
            await Assert.That(wrongPasswordResult.IsSuccess).IsFalse();

            await Assert.That(unknownEmailResult.StatusCode).IsEqualTo(StatusCodes.Status401Unauthorized);
            await Assert.That(wrongPasswordResult.StatusCode).IsEqualTo(StatusCodes.Status401Unauthorized);

            await Assert.That(unknownEmailResult.ErrorCode).IsEqualTo(ErrorCodes.Auth.InvalidCredentials);
            await Assert.That(wrongPasswordResult.ErrorCode).IsEqualTo(ErrorCodes.Auth.InvalidCredentials);

            await Assert.That(unknownEmailResult.Message).IsEqualTo("Invalid email or password.");
            await Assert.That(wrongPasswordResult.Message).IsEqualTo(unknownEmailResult.Message);

            await Assert.That(unknownEmailResult.Data).IsNull();
            await Assert.That(wrongPasswordResult.Data).IsNull();

            await Assert.That(await Context.RefreshTokens.CountAsync()).IsEqualTo(0);
        }

        [Test]
        public async Task LoginAsync_WhenCredentialsAreInvalid_PerformsPasswordVerificationForBothPaths()
        {
            // Arrange
            var user = await CreateUserAsync("timing@example.com");
            var hasher = CountingHasher;

            // Act
            await Sut.LoginAsync(new LoginCommand("missing@example.com", Password));
            await Sut.LoginAsync(new LoginCommand(user.Email!, "WrongPassword123!"));

            // Assert
            await Assert.That(hasher.VerifyCount).IsGreaterThan(1);
        }

        [Test]
        public async Task LoginAsync_WhenAccountIsLockedOut_ReturnsAccountLocked()
        {
            // Arrange
            var user = await CreateUserAsync("locked@example.com");
            await UserManager.SetLockoutEnabledAsync(user, true);
            await UserManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddMinutes(15));

            // Act
            var result = await Sut.LoginAsync(new LoginCommand(user.Email!, Password));

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status423Locked);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.Auth.AccountLocked);
            await Assert.That(result.Data).IsNull();
        }

        [Test]
        public async Task LoginAsync_WhenMaxFailedAttemptsReached_LocksAccount()
        {
            // Arrange
            var user = await CreateUserAsync("bruteforce@example.com");
            await UserManager.SetLockoutEnabledAsync(user, true);

            // Act & Assert
            for (var attempt = 1; attempt <= MaxFailedAccessAttempts; attempt++)
            {
                var result = await Sut.LoginAsync(new LoginCommand(user.Email!, "WrongPassword123!"));

                if (attempt < MaxFailedAccessAttempts)
                {
                    await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status401Unauthorized);
                    await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.Auth.InvalidCredentials);
                }
                else
                {
                    await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status423Locked);
                    await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.Auth.AccountLocked);
                }
            }

            await Assert.That(await UserManager.IsLockedOutAsync(user)).IsTrue();
        }

        [Test]
        public async Task LoginAsync_WhenPasswordIsCorrect_ResetsAccessFailedCount()
        {
            // Arrange
            var user = await CreateUserAsync("reset-failures@example.com");
            await UserManager.SetLockoutEnabledAsync(user, true);

            var failedResult = await Sut.LoginAsync(new LoginCommand(user.Email!, "WrongPassword123!"));

            // Assert
            await Assert.That(failedResult.ErrorCode).IsEqualTo(ErrorCodes.Auth.InvalidCredentials);
            await Assert.That(await UserManager.GetAccessFailedCountAsync(user)).IsEqualTo(1);

            // Act
            var successResult = await Sut.LoginAsync(new LoginCommand(user.Email!, Password));

            // Assert
            await Assert.That(successResult.IsSuccess).IsTrue();
            await Assert.That(await UserManager.GetAccessFailedCountAsync(user)).IsEqualTo(0);
        }

        [Test]
        public async Task LoginAsync_WhenEmailIsNotConfirmed_ReturnsGenericInvalidCredentials()
        {
            // Arrange
            var user = await CreateUserAsync("unconfirmed@example.com", emailConfirmed: false);

            // Act
            var result = await Sut.LoginAsync(new LoginCommand(user.Email!, Password));

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status401Unauthorized);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.Auth.InvalidCredentials);
            await Assert.That(result.Message).IsEqualTo("Invalid email or password.");
            await Assert.That(result.Data).IsNull();

            await Assert.That(await Context.RefreshTokens.CountAsync()).IsEqualTo(0);
        }

        [Test]
        public async Task LoginAsync_WhenAccountIsBanned_BlocksAuthentication()
        {
            // Arrange
            var user = await CreateUserAsync("banned@example.com");
            user.IsBanned = true;
            user.BannedUntil = null;
            await Context.SaveChangesAsync();

            // Act
            var result = await Sut.LoginAsync(new LoginCommand(user.Email!, Password));

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status403Forbidden);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.Auth.AccountBanned);
            await Assert.That(result.Message).IsEqualTo("Account is banned.");
            await Assert.That(result.Data).IsNull();

            await Assert.That(await Context.RefreshTokens.CountAsync()).IsEqualTo(0);
        }

        [Test]
        public async Task LoginAsync_WhenAccountBanHasExpired_AllowsAuthentication()
        {
            // Arrange
            var user = await CreateUserAsync("expired-ban@example.com");
            user.IsBanned = true;
            user.BannedUntil = DateTime.UtcNow.AddMinutes(-1);
            await Context.SaveChangesAsync();

            // Act
            var result = await Sut.LoginAsync(new LoginCommand(user.Email!, Password));

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
        }

        [Test]
        public async Task LoginAsync_WhenAccountIsBannedAndPasswordIsWrong_ReturnsGenericInvalidCredentials()
        {
            // Arrange
            var user = await CreateUserAsync("banned-wrong-password@example.com");
            user.IsBanned = true;
            user.BannedUntil = null;
            await Context.SaveChangesAsync();

            // Act
            var result = await Sut.LoginAsync(new LoginCommand(user.Email!, "WrongPassword123!"));

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status401Unauthorized);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.Auth.InvalidCredentials);
            await Assert.That(result.Data).IsNull();
        }

        private ClaimsPrincipal ValidateAccessToken(string accessToken)
        {
            var handler = new JwtSecurityTokenHandler();

            return handler.ValidateToken(
                accessToken,
                new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = _jwtOptions.Issuer,
                    ValidateAudience = true,
                    ValidAudience = _jwtOptions.Audience,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.SecretKey)),
                    ClockSkew = TimeSpan.Zero
                },
                out _);
        }

        #endregion

        #region LogoutAsync Tests

        [Test]
        public async Task LogoutAsync_WhenRefreshTokenIsValid_RevokesTokenAndRejectsSubsequentRefresh()
        {
            // Arrange
            var user = await CreateUserAsync("logout@example.com");
            var loginResult = await Sut.LoginAsync(new LoginCommand(user.Email!, Password));
            var refreshToken = loginResult.Data!.RefreshToken;

            // Act
            var logoutResult = await Sut.LogoutAsync(refreshToken);

            // Assert
            await Assert.That(logoutResult.IsSuccess).IsTrue();
            await Assert.That(logoutResult.StatusCode).IsEqualTo(StatusCodes.Status200OK);

            var storedToken = await Context.RefreshTokens
                .AsNoTracking()
                .SingleAsync(token => token.Token == refreshToken);

            await Assert.That(storedToken.IsRevoked).IsTrue();
            await Assert.That(storedToken.RevokedAt).IsNotNull();

            var refreshResult = await Sut.RefreshAsync(refreshToken);

            await Assert.That(refreshResult.IsSuccess).IsFalse();
            await Assert.That(refreshResult.StatusCode).IsEqualTo(StatusCodes.Status401Unauthorized);
            await Assert.That(refreshResult.ErrorCode).IsEqualTo(ErrorCodes.Auth.InvalidRefreshToken);
            await Assert.That(refreshResult.Data).IsNull();

            await Assert.That(await Context.RefreshTokens.CountAsync()).IsEqualTo(1);
        }

        #endregion

        #region RefreshAsync Tests

        [Test]
        public async Task RefreshAsync_WhenRefreshTokenIsConsumed_RejectsSubsequentRefresh()
        {
            // Arrange
            var user = await CreateUserAsync("consumed@example.com");
            var loginResult = await Sut.LoginAsync(new LoginCommand(user.Email!, Password));
            var refreshToken = loginResult.Data!.RefreshToken;

            // Act
            var firstRefreshResult = await Sut.RefreshAsync(refreshToken);
            var secondRefreshResult = await Sut.RefreshAsync(refreshToken);

            // Assert
            await Assert.That(firstRefreshResult.IsSuccess).IsTrue();
            await Assert.That(firstRefreshResult.Data).IsNotNull();
            await Assert.That(firstRefreshResult.Data!.RefreshToken).IsNotEqualTo(refreshToken);

            var consumedToken = await Context.RefreshTokens
                .AsNoTracking()
                .SingleAsync(token => token.Token == refreshToken);

            await Assert.That(consumedToken.IsRevoked).IsTrue();
            await Assert.That(consumedToken.RevokedAt).IsNotNull();

            await Assert.That(secondRefreshResult.IsSuccess).IsFalse();
            await Assert.That(secondRefreshResult.StatusCode).IsEqualTo(StatusCodes.Status401Unauthorized);
            await Assert.That(secondRefreshResult.ErrorCode).IsEqualTo(ErrorCodes.Auth.InvalidRefreshToken);
            await Assert.That(secondRefreshResult.Data).IsNull();
        }

        [Test]
        public async Task RefreshAsync_WhenEmailIsNotConfirmed_ConsumesTokenAndRejectsRefresh()
        {
            // Arrange
            var user = await CreateUserAsync("unconfirmed-refresh@example.com");
            var loginResult = await Sut.LoginAsync(new LoginCommand(user.Email!, Password));
            var refreshToken = loginResult.Data!.RefreshToken;

            user.EmailConfirmed = false;
            await Context.SaveChangesAsync();

            // Act
            var refreshResult = await Sut.RefreshAsync(refreshToken);

            // Assert
            await Assert.That(refreshResult.IsSuccess).IsFalse();
            await Assert.That(refreshResult.StatusCode).IsEqualTo(StatusCodes.Status403Forbidden);
            await Assert.That(refreshResult.ErrorCode).IsEqualTo(ErrorCodes.Auth.EmailNotConfirmed);
            await Assert.That(refreshResult.Data).IsNull();

            Context.ChangeTracker.Clear();

            var consumedToken = await Context.RefreshTokens
                .AsNoTracking()
                .SingleAsync(token => token.Token == refreshToken);

            await Assert.That(consumedToken.IsRevoked).IsTrue();
        }

        [Test]
        public async Task RefreshAsync_WhenAccountIsBanned_RevokesAllTokensAndRejectsRefresh()
        {
            // Arrange
            var user = await CreateUserAsync("banned-refresh@example.com");
            var loginResult = await Sut.LoginAsync(new LoginCommand(user.Email!, Password));
            var refreshToken = loginResult.Data!.RefreshToken;
            var tokenService = _scope.ServiceProvider.GetRequiredService<ITokenService>();

            await tokenService.CreateRefreshTokenAsync(user.Id);

            user.IsBanned = true;
            user.BannedUntil = null;
            await Context.SaveChangesAsync();

            // Act
            var refreshResult = await Sut.RefreshAsync(refreshToken);

            // Assert
            await Assert.That(refreshResult.IsSuccess).IsFalse();
            await Assert.That(refreshResult.StatusCode).IsEqualTo(StatusCodes.Status403Forbidden);
            await Assert.That(refreshResult.ErrorCode).IsEqualTo(ErrorCodes.Auth.AccountBanned);
            await Assert.That(refreshResult.Data).IsNull();

            Context.ChangeTracker.Clear();

            var storedTokens = await Context.RefreshTokens
                .AsNoTracking()
                .Where(token => token.UserId == user.Id)
                .ToListAsync();

            await Assert.That(storedTokens.Count).IsEqualTo(2);
            await Assert.That(storedTokens.All(token => token.IsRevoked)).IsTrue();
        }

        [Test]
        public async Task RefreshAsync_WhenRefreshTokenIsExpired_RejectsToken()
        {
            // Arrange
            var user = await CreateUserAsync("expired@example.com");
            var loginResult = await Sut.LoginAsync(new LoginCommand(user.Email!, Password));
            var refreshToken = loginResult.Data!.RefreshToken;

            var storedToken = await Context.RefreshTokens
                .SingleAsync(token => token.Token == refreshToken);

            storedToken.ExpiredAt = DateTime.UtcNow.AddMinutes(-1);
            await Context.SaveChangesAsync();

            // Act
            var refreshResult = await Sut.RefreshAsync(refreshToken);

            // Assert
            await Assert.That(refreshResult.IsSuccess).IsFalse();
            await Assert.That(refreshResult.StatusCode).IsEqualTo(StatusCodes.Status401Unauthorized);
            await Assert.That(refreshResult.ErrorCode).IsEqualTo(ErrorCodes.Auth.InvalidRefreshToken);
            await Assert.That(refreshResult.Data).IsNull();

            await Assert.That(await Context.RefreshTokens.CountAsync()).IsEqualTo(1);
        }

        #endregion

        #region ConsumeRefreshTokenAsync Tests

        [Test]
        public async Task ConsumeRefreshTokenAsync_WhenCalledConcurrently_AllowsOnlyOneConsumption()
        {
            // Arrange
            var user = await CreateUserAsync("concurrent@example.com");
            var loginResult = await Sut.LoginAsync(new LoginCommand(user.Email!, Password));
            var refreshToken = loginResult.Data!.RefreshToken;

            using var firstScope = _serviceProvider.CreateScope();
            using var secondScope = _serviceProvider.CreateScope();

            var firstTokenService = firstScope.ServiceProvider.GetRequiredService<ITokenService>();
            var secondTokenService = secondScope.ServiceProvider.GetRequiredService<ITokenService>();

            // Act
            var results = await Task.WhenAll(
                firstTokenService.ConsumeRefreshTokenAsync(refreshToken),
                secondTokenService.ConsumeRefreshTokenAsync(refreshToken));

            // Assert
            await Assert.That(results.Count(result => result is not null)).IsEqualTo(1);

            Context.ChangeTracker.Clear();

            var consumedToken = await Context.RefreshTokens
                .AsNoTracking()
                .SingleAsync(token => token.Token == refreshToken);

            await Assert.That(consumedToken.IsRevoked).IsTrue();
            await Assert.That(consumedToken.RevokedAt).IsNotNull();
        }

        #endregion

        #region RevokeRefreshTokenAsync Tests

        [Test]
        public async Task RevokeRefreshTokenAsync_WhenTokenIsActive_ReturnsTrueAndRevokesToken()
        {
            // Arrange
            var user = await CreateUserAsync("revoke-active@example.com");
            var loginResult = await Sut.LoginAsync(new LoginCommand(user.Email!, Password));
            var refreshToken = loginResult.Data!.RefreshToken;
            var tokenService = _scope.ServiceProvider.GetRequiredService<ITokenService>();

            // Act
            var revoked = await tokenService.RevokeRefreshTokenAsync(refreshToken);

            // Assert
            await Assert.That(revoked).IsTrue();

            Context.ChangeTracker.Clear();

            var storedToken = await Context.RefreshTokens
                .AsNoTracking()
                .SingleAsync(token => token.Token == refreshToken);

            await Assert.That(storedToken.IsRevoked).IsTrue();
            await Assert.That(storedToken.RevokedAt).IsNotNull();
            await Assert.That(storedToken.UpdatedAt).IsNotNull();
        }

        [Test]
        public async Task RevokeRefreshTokenAsync_WhenTokenIsAlreadyRevoked_ReturnsFalse()
        {
            // Arrange
            var user = await CreateUserAsync("revoke-twice@example.com");
            var loginResult = await Sut.LoginAsync(new LoginCommand(user.Email!, Password));
            var refreshToken = loginResult.Data!.RefreshToken;
            var tokenService = _scope.ServiceProvider.GetRequiredService<ITokenService>();

            // Act
            var firstRevocation = await tokenService.RevokeRefreshTokenAsync(refreshToken);
            var secondRevocation = await tokenService.RevokeRefreshTokenAsync(refreshToken);

            // Assert
            await Assert.That(firstRevocation).IsTrue();
            await Assert.That(secondRevocation).IsFalse();
        }

        [Test]
        public async Task RevokeRefreshTokenAsync_WhenTokenDoesNotExist_ReturnsFalse()
        {
            // Arrange
            var tokenService = _scope.ServiceProvider.GetRequiredService<ITokenService>();

            // Act
            var revoked = await tokenService.RevokeRefreshTokenAsync("non-existent-token");

            // Assert
            await Assert.That(revoked).IsFalse();
        }

        [Test]
        public async Task RevokeRefreshTokenAsync_WhenCalledConcurrently_RevokesOnlyOnce()
        {
            // Arrange
            var user = await CreateUserAsync("revoke-concurrent@example.com");
            var loginResult = await Sut.LoginAsync(new LoginCommand(user.Email!, Password));
            var refreshToken = loginResult.Data!.RefreshToken;

            using var firstScope = _serviceProvider.CreateScope();
            using var secondScope = _serviceProvider.CreateScope();

            var firstTokenService = firstScope.ServiceProvider.GetRequiredService<ITokenService>();
            var secondTokenService = secondScope.ServiceProvider.GetRequiredService<ITokenService>();

            // Act
            var results = await Task.WhenAll(
                firstTokenService.RevokeRefreshTokenAsync(refreshToken),
                secondTokenService.RevokeRefreshTokenAsync(refreshToken));

            // Assert
            await Assert.That(results.Count(result => result)).IsEqualTo(1);

            Context.ChangeTracker.Clear();

            var storedToken = await Context.RefreshTokens
                .AsNoTracking()
                .SingleAsync(token => token.Token == refreshToken);

            await Assert.That(storedToken.IsRevoked).IsTrue();
        }

        #endregion

        #region RevokeAllUserRefreshTokensAsync Tests

        [Test]
        public async Task RevokeAllUserRefreshTokensAsync_WhenUserHasActiveTokens_RevokesOnlyThatUsersActiveTokens()
        {
            // Arrange
            var user = await CreateUserAsync("revoke-all@example.com");
            var otherUser = await CreateUserAsync("revoke-all-other@example.com");
            var tokenService = _scope.ServiceProvider.GetRequiredService<ITokenService>();

            var firstToken = await tokenService.CreateRefreshTokenAsync(user.Id);
            var alreadyRevokedToken = await tokenService.CreateRefreshTokenAsync(user.Id);
            var otherUserToken = await tokenService.CreateRefreshTokenAsync(otherUser.Id);

            await tokenService.RevokeRefreshTokenAsync(alreadyRevokedToken.Token);

            var alreadyRevokedAt = await Context.RefreshTokens
                .AsNoTracking()
                .Where(t => t.Id == alreadyRevokedToken.Id)
                .Select(t => t.RevokedAt)
                .SingleAsync();

            // Act
            await tokenService.RevokeAllUserRefreshTokensAsync(user.Id);

            // Assert
            Context.ChangeTracker.Clear();

            var tokens = await Context.RefreshTokens
                .AsNoTracking()
                .Where(t => t.Id == firstToken.Id || t.Id == alreadyRevokedToken.Id || t.Id == otherUserToken.Id)
                .ToDictionaryAsync(t => t.Id);

            await Assert.That(tokens[firstToken.Id].IsRevoked).IsTrue();
            await Assert.That(tokens[firstToken.Id].RevokedAt).IsNotNull();
            await Assert.That(tokens[firstToken.Id].UpdatedAt).IsNotNull();

            await Assert.That(tokens[alreadyRevokedToken.Id].IsRevoked).IsTrue();
            await Assert.That(tokens[alreadyRevokedToken.Id].RevokedAt).IsEqualTo(alreadyRevokedAt);

            await Assert.That(tokens[otherUserToken.Id].IsRevoked).IsFalse();
            await Assert.That(tokens[otherUserToken.Id].RevokedAt).IsNull();
        }

        #endregion

        #region Shared Test Helpers

        private async Task CreateRoleAsync(string roleName)
        {
            var result = await RoleManager.CreateAsync(new IdentityRole<Guid>
            {
                Id = Guid.CreateVersion7(),
                Name = roleName
            });

            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Could not create role '{roleName}': {string.Join(", ", result.Errors.Select(error => error.Description))}");
            }
        }

        private async Task<User> CreateUserAsync(
            string email,
            bool emailConfirmed = true,
            string role = "User",
            string password = Password)
        {
            var user = new User
            {
                Id = Guid.CreateVersion7(),
                UserName = email,
                Email = email,
                FirstName = "Test",
                LastName = "User",
                EmailConfirmed = emailConfirmed
            };

            var createResult = await UserManager.CreateAsync(user, password);
            if (!createResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Could not create user '{email}': {string.Join(", ", createResult.Errors.Select(error => error.Description))}");
            }

            var roleResult = await UserManager.AddToRoleAsync(user, role);
            if (!roleResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Could not assign role '{role}' to '{email}': {string.Join(", ", roleResult.Errors.Select(error => error.Description))}");
            }

            return user;
        }

        private sealed class AuthTestDbContext : AppDbContext
        {
            public AuthTestDbContext(DbContextOptions<AppDbContext> options) : base(options)
            {
            }

            protected override void OnModelCreating(ModelBuilder builder)
            {
                base.OnModelCreating(builder);

                builder.Ignore<Country>();
                builder.Ignore<Place>();
                builder.Ignore<Trip>();
                builder.Ignore<TripSegment>();
                builder.Ignore<VisitedCountry>();
            }
        }

        private sealed class CountingPasswordHasher : IPasswordHasher<User>
        {
            private readonly PasswordHasher<User> _inner = new();

            public int VerifyCount { get; private set; }
            public int DummyHashCount { get; private set; }

            public string HashPassword(User user, string password)
            {
                if (password == "dummy-password-not-used")
                {
                    DummyHashCount++;
                }

                return _inner.HashPassword(user, password);
            }

            public PasswordVerificationResult VerifyHashedPassword(
                User user,
                string hashedPassword,
                string providedPassword)
            {
                VerifyCount++;

                return _inner.VerifyHashedPassword(user, hashedPassword, providedPassword);
            }
        }

        #endregion
    }
}
