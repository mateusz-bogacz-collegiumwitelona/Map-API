using Domain.Const;
using Domain.Models;
using Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
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
        private const string Password = "Password123!";
        private const string JwtSecretKey = "test-secret-key-with-at-least-32-characters";
        private const string JwtIssuer = "MAP-API-Tests";
        private const string JwtAudience = "MAP-API-Tests";

        private ServiceProvider _serviceProvider = null!;
        private IServiceScope _scope = null!;
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

            services.AddLogging();
            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase($"AuthServiceTests_{Guid.NewGuid():N}"));

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
        }

        [Test]
        public async Task LoginAsync_WhenCredentialsAreValid_ReturnsAuthResponseWithClaimsAndPersistedRefreshToken()
        {
            // Arrange
            var user = await CreateUserAsync("confirmed@example.com");

            // Act
            var result = await Sut.LoginAsync(user.Email!, Password);

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
            var unknownEmailResult = await Sut.LoginAsync("unknown@example.com", Password);
            var wrongPasswordResult = await Sut.LoginAsync(user.Email!, "WrongPassword123!");

            // Assert
            await Assert.That(unknownEmailResult.IsSuccess).IsFalse();
            await Assert.That(wrongPasswordResult.IsSuccess).IsFalse();

            await Assert.That(unknownEmailResult.StatusCode).IsEqualTo(StatusCodes.Status401Unauthorized);
            await Assert.That(wrongPasswordResult.StatusCode).IsEqualTo(StatusCodes.Status401Unauthorized);

            await Assert.That(unknownEmailResult.ErrorCode).IsEqualTo(ErrorCodes.InvalidCredentials);
            await Assert.That(wrongPasswordResult.ErrorCode).IsEqualTo(ErrorCodes.InvalidCredentials);

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
            await Sut.LoginAsync("missing@example.com", Password);
            await Sut.LoginAsync(user.Email!, "WrongPassword123!");

            // Assert
            await Assert.That(hasher.VerifyCount).IsGreaterThan(1);
        }

        [Test]
        public async Task LoginAsync_WhenEmailIsNotConfirmed_BlocksAuthentication()
        {
            // Arrange
            var user = await CreateUserAsync("unconfirmed@example.com", emailConfirmed: false);

            // Act
            var result = await Sut.LoginAsync(user.Email!, Password);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status403Forbidden);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.EmailNotConfirmed);
            await Assert.That(result.Message).IsEqualTo("Email address is not confirmed.");
            await Assert.That(result.Data).IsNull();

            await Assert.That(await Context.RefreshTokens.CountAsync()).IsEqualTo(0);
        }

        [Test]
        public async Task LogoutAsync_WhenRefreshTokenIsValid_RevokesTokenAndRejectsSubsequentRefresh()
        {
            // Arrange
            var user = await CreateUserAsync("logout@example.com");
            var loginResult = await Sut.LoginAsync(user.Email!, Password);
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
            await Assert.That(refreshResult.ErrorCode).IsEqualTo(ErrorCodes.InvalidRefreshToken);
            await Assert.That(refreshResult.Data).IsNull();

            await Assert.That(await Context.RefreshTokens.CountAsync()).IsEqualTo(1);
        }

        [Test]
        public async Task RefreshAsync_WhenRefreshTokenIsExpired_RejectsToken()
        {
            // Arrange
            var user = await CreateUserAsync("expired@example.com");
            var loginResult = await Sut.LoginAsync(user.Email!, Password);
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
            await Assert.That(refreshResult.ErrorCode).IsEqualTo(ErrorCodes.InvalidRefreshToken);
            await Assert.That(refreshResult.Data).IsNull();

            await Assert.That(await Context.RefreshTokens.CountAsync()).IsEqualTo(1);
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

        private sealed class CountingPasswordHasher : IPasswordHasher<User>
        {
            private readonly PasswordHasher<User> _inner = new();

            public int VerifyCount { get; private set; }

            public string HashPassword(User user, string password)
                => _inner.HashPassword(user, password);

            public PasswordVerificationResult VerifyHashedPassword(
                User user,
                string hashedPassword,
                string providedPassword)
            {
                VerifyCount++;

                return _inner.VerifyHashedPassword(user, hashedPassword, providedPassword);
            }
        }
    }
}
