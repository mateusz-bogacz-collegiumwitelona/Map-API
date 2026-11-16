using Api.Configuration;
using Domain.Common;
using Domain.Const;
using Domain.Models;
using Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;
using System.Text.Json;

namespace Tests.Other
{
    public class AuthenticationConfigurationTests
    {
        private AppDbContext _context = null!;

        [Before(Test)]
        public void Setup()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: $"AuthConfigTests_{Guid.NewGuid()}")
                .Options;

            _context = new AppDbContext(options);
        }

        [After(Test)]
        public async Task CleanupAsync()
        {
            await _context.DisposeAsync();
        }

        private TokenValidatedContext CreateTokenValidatedContext(Guid? userId)
        {
            var services = new ServiceCollection();
            services.AddSingleton(_context);
            var serviceProvider = services.BuildServiceProvider();

            var httpContext = new DefaultHttpContext
            {
                RequestServices = serviceProvider
            };

            var claims = new List<Claim>();
            if (userId.HasValue)
            {
                claims.Add(new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()));
            }

            var identity = new ClaimsIdentity(claims, JwtBearerDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);
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
            DateTime? bannedUntil = null)
        {
            var user = new User
            {
                Id = Guid.NewGuid(),
                FirstName = "Jan",
                LastName = "Kowalski",
                IsBanned = isBanned,
                BannedUntil = bannedUntil
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return user;
        }

        #region ValidateUserIsActiveAsync Tests

        [Test]
        public async Task ValidateUserIsActiveAsync_WhenUserIsActiveAndNotBanned_ShouldSucceed()
        {
            // Arrange
            var user = await AddUserAsync(isBanned: false);
            var context = CreateTokenValidatedContext(user.Id);

            // Act
            await AuthenticationConfiguration.ValidateUserIsActiveAsync(context);

            // Assert
            await Assert.That(context.Result).IsNull();
            await Assert.That(context.HttpContext.Items.ContainsKey("AccountBanned")).IsFalse();
        }

        [Test]
        public async Task ValidateUserIsActiveAsync_WhenTemporaryBanHasExpired_ShouldSucceed()
        {
            // Arrange
            var user = await AddUserAsync(
                isBanned: true,
                bannedUntil: DateTime.UtcNow.AddMinutes(-1));
            var context = CreateTokenValidatedContext(user.Id);

            // Act
            await AuthenticationConfiguration.ValidateUserIsActiveAsync(context);

            // Assert
            await Assert.That(context.Result).IsNull();
            await Assert.That(context.HttpContext.Items.ContainsKey("AccountBanned")).IsFalse();
        }

        [Test]
        public async Task ValidateUserIsActiveAsync_WhenUserHasActiveTemporaryBan_ShouldFailAndSetBannedItem()
        {
            // Arrange
            var user = await AddUserAsync(
                isBanned: true,
                bannedUntil: DateTime.UtcNow.AddDays(1));
            var context = CreateTokenValidatedContext(user.Id);

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
                bannedUntil: null);
            var context = CreateTokenValidatedContext(user.Id);

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
            var context = CreateTokenValidatedContext(Guid.NewGuid());

            // Act
            await AuthenticationConfiguration.ValidateUserIsActiveAsync(context);

            // Assert
            await Assert.That(context.Result).IsNotNull();
            await Assert.That(context.Result!.Failure!.Message).IsEqualTo("User account no longer exists.");
            await Assert.That(context.HttpContext.Items.ContainsKey("AccountBanned")).IsFalse();
        }

        [Test]
        public async Task ValidateUserIsActiveAsync_WhenUserIdClaimIsMissingOrInvalid_ShouldFail()
        {
            // Arrange
            var context = CreateTokenValidatedContext(null);

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
    }
}
