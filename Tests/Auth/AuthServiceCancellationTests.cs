using Domain.Models;
using Microsoft.AspNetCore.Identity;
using Services.Accessors.Interfaces;
using Services.Commands.Auth;
using Services.Interfaces.Auth;
using Services.Services.Auth;

namespace Tests.Auth;

public class AuthServiceCancellationTests
{
    [Test]
    public async Task LoginAsync_WhenMethodTokenAlreadyCancelled_ThrowsBeforeGeneratingTokens()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        var accessor = new StubCancellationTokenAccessor();
        var tokenService = new RecordingTokenService();
        var sut = CreateSut(tokenService, accessor);

        // Act & Assert
        await Assert.That(async () =>
        {
            await sut.LoginAsync(new LoginCommand("unknown@example.com", "Password123!"), cancellationTokenSource.Token);
        }).Throws<OperationCanceledException>();

        await Assert.That(tokenService.GenerateAccessTokenCalled).IsFalse();
        await Assert.That(tokenService.CreateRefreshTokenCalled).IsFalse();
    }

    [Test]
    public async Task RefreshAsync_WhenMethodTokenAlreadyCancelled_ThrowsBeforeConsumingToken()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        var accessor = new StubCancellationTokenAccessor();
        var tokenService = new RecordingTokenService();
        var sut = CreateSut(tokenService, accessor);

        // Act & Assert
        await Assert.That(async () =>
        {
            await sut.RefreshAsync("refresh-token", cancellationTokenSource.Token);
        }).Throws<OperationCanceledException>();

        await Assert.That(tokenService.ConsumeRefreshTokenCalled).IsFalse();
    }

    [Test]
    public async Task LogoutAsync_WhenAccessorTokenAlreadyCancelled_ThrowsBeforeCallingTokenService()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        var accessor = new StubCancellationTokenAccessor { Token = cancellationTokenSource.Token };
        var tokenService = new RecordingTokenService();
        var sut = CreateSut(tokenService, accessor);

        // Act & Assert
        await Assert.That(async () =>
        {
            await sut.LogoutAsync("refresh-token");
        }).Throws<OperationCanceledException>();

        await Assert.That(tokenService.WasCalled).IsFalse();
    }

    [Test]
    public async Task LogoutAsync_WhenAccessorTokenCancelledAndMethodTokenProvided_Throws()
    {
        // Arrange
        using var methodCancellationTokenSource = new CancellationTokenSource();
        using var accessorCancellationTokenSource = new CancellationTokenSource();
        accessorCancellationTokenSource.Cancel();

        var accessor = new StubCancellationTokenAccessor { Token = accessorCancellationTokenSource.Token };
        var tokenService = new RecordingTokenService();
        var sut = CreateSut(tokenService, accessor);

        // Act & Assert
        await Assert.That(async () =>
        {
            await sut.LogoutAsync("refresh-token", methodCancellationTokenSource.Token);
        }).Throws<OperationCanceledException>();

        await Assert.That(tokenService.WasCalled).IsFalse();
    }

    [Test]
    public async Task LogoutAsync_WhenNoMethodTokenProvided_UsesAccessorToken()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        var accessor = new StubCancellationTokenAccessor { Token = cancellationTokenSource.Token };
        var tokenService = new RecordingTokenService();
        var sut = CreateSut(tokenService, accessor);

        // Act
        await sut.LogoutAsync("refresh-token");

        // Assert
        await Assert.That(tokenService.WasCalled).IsTrue();
        await Assert.That(tokenService.LastCancellationToken).IsEqualTo(cancellationTokenSource.Token);
    }

    [Test]
    public async Task LogoutAsync_WhenMethodTokenProvided_PrefersMethodToken()
    {
        // Arrange
        using var methodCancellationTokenSource = new CancellationTokenSource();
        using var accessorCancellationTokenSource = new CancellationTokenSource();
        var accessor = new StubCancellationTokenAccessor { Token = accessorCancellationTokenSource.Token };
        var tokenService = new RecordingTokenService();
        var sut = CreateSut(tokenService, accessor);

        // Act
        await sut.LogoutAsync("refresh-token", methodCancellationTokenSource.Token);

        // Assert
        await Assert.That(tokenService.WasCalled).IsTrue();
        await Assert.That(tokenService.LastCancellationToken).IsEqualTo(methodCancellationTokenSource.Token);
    }

    private static AuthService CreateSut(ITokenService tokenService, ICancellationTokenAccessor accessor)
        => new(null!, tokenService, new PasswordHasher<User>(), accessor);

    private sealed class StubCancellationTokenAccessor : ICancellationTokenAccessor
    {
        public CancellationToken Token { get; set; }
    }

    private sealed class RecordingTokenService : ITokenService
    {
        public bool WasCalled { get; private set; }

        public bool GenerateAccessTokenCalled { get; private set; }

        public bool CreateRefreshTokenCalled { get; private set; }

        public bool ConsumeRefreshTokenCalled { get; private set; }

        public CancellationToken LastCancellationToken { get; private set; }

        public (string Token, DateTime ExpiresAt) GenerateAccessToken(User user, IEnumerable<string> roles)
        {
            GenerateAccessTokenCalled = true;
            return ("access-token", DateTime.UtcNow);
        }

        public string GenerateRefreshToken()
            => throw new NotSupportedException();

        public Task<RefreshToken> CreateRefreshTokenAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            CreateRefreshTokenCalled = true;

            return Task.FromResult(new RefreshToken
            {
                UserId = userId,
                Token = "refresh-token",
                ExpiredAt = DateTime.UtcNow.AddDays(1)
            });
        }

        public Task<RefreshToken?> ConsumeRefreshTokenAsync(string token, CancellationToken cancellationToken = default)
        {
            ConsumeRefreshTokenCalled = true;
            return Task.FromResult<RefreshToken?>(null);
        }

        public Task<bool> RevokeRefreshTokenAsync(string token, CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            LastCancellationToken = cancellationToken;
            return Task.FromResult(true);
        }

        public Task RevokeAllUserRefreshTokensAsync(Guid userId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
