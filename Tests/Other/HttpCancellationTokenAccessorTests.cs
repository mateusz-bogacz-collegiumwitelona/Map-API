using Microsoft.AspNetCore.Http;
using Services.Accessors;
using Services.Accessors.Interfaces;

namespace Tests.Other
{
    public class HttpCancellationTokenAccessorTests
    {
        private class FakeHttpContextAccessor : IHttpContextAccessor
        {
            public HttpContext? HttpContext { get; set; }
        }

        #region Token Property Tests

        [Test]
        public async Task Token_WhenHttpContextIsNull_ReturnsCancellationTokenNone()
        {
            // Arrange
            var accessorMock = new FakeHttpContextAccessor { HttpContext = null };
            ICancellationTokenAccessor sut = new HttpCancellationTokenAccessor(accessorMock);

            // Act
            var token = sut.Token;

            // Assert
            await Assert.That(token).IsEqualTo(CancellationToken.None);
            await Assert.That(token.CanBeCanceled).IsFalse();
            await Assert.That(token.IsCancellationRequested).IsFalse();
        }

        [Test]
        public async Task Token_WhenHttpContextExistsAndNotAborted_ReturnsActiveToken()
        {
            // Arrange
            using var cts = new CancellationTokenSource();
            var httpContext = new DefaultHttpContext
            {
                RequestAborted = cts.Token
            };

            var accessorMock = new FakeHttpContextAccessor { HttpContext = httpContext };
            ICancellationTokenAccessor sut = new HttpCancellationTokenAccessor(accessorMock);

            // Act
            var token = sut.Token;

            // Assert
            await Assert.That(token.CanBeCanceled).IsTrue();
            await Assert.That(token.IsCancellationRequested).IsFalse();
            await Assert.That(token).IsEqualTo(cts.Token);
        }

        [Test]
        public async Task Token_WhenRequestIsAborted_ReflectsCancellationState()
        {
            // Arrange
            using var cts = new CancellationTokenSource();
            var httpContext = new DefaultHttpContext
            {
                RequestAborted = cts.Token
            };

            var accessorMock = new FakeHttpContextAccessor { HttpContext = httpContext };
            ICancellationTokenAccessor sut = new HttpCancellationTokenAccessor(accessorMock);

            cts.Cancel();

            // Act
            var token = sut.Token;

            // Assert
            await Assert.That(token.IsCancellationRequested).IsTrue();
            await Assert.That(token).IsEqualTo(cts.Token);
        }
        #endregion

        #region ResolveCancellationToken
        [Test]
        public async Task ResolveCancellationToken_WhenMethodTokenCanBeCanceled_ReturnsMethodToken()
        {
            // Arrange
            using var methodCts = new CancellationTokenSource();
            using var httpCts = new CancellationTokenSource();
            var httpContext = new DefaultHttpContext { RequestAborted = httpCts.Token };

            var accessorMock = new FakeHttpContextAccessor { HttpContext = httpContext };
            ICancellationTokenAccessor sut = new HttpCancellationTokenAccessor(accessorMock);

            // Act
            var resolvedToken = sut.ResolveCancellationToken(methodCts.Token);

            // Assert
            await Assert.That(resolvedToken).IsEqualTo(methodCts.Token);
            await Assert.That(resolvedToken.CanBeCanceled).IsTrue();
        }

        [Test]
        public async Task ResolveCancellationToken_WhenMethodTokenCannotBeCanceled_ReturnsAccessorToken()
        {
            // Arrange
            using var httpCts = new CancellationTokenSource();
            var httpContext = new DefaultHttpContext { RequestAborted = httpCts.Token };

            var accessorMock = new FakeHttpContextAccessor { HttpContext = httpContext };
            ICancellationTokenAccessor sut = new HttpCancellationTokenAccessor(accessorMock);

            // Act
            var resolvedToken = sut.ResolveCancellationToken(CancellationToken.None);

            // Assert
            await Assert.That(resolvedToken).IsEqualTo(httpCts.Token);
            await Assert.That(resolvedToken.CanBeCanceled).IsTrue();
        }

        [Test]
        public async Task ResolveCancellationToken_WhenMethodTokenCannotBeCanceledAndHttpContextIsNull_ReturnsNone()
        {
            // Arrange
            var accessorMock = new FakeHttpContextAccessor { HttpContext = null };
            ICancellationTokenAccessor sut = new HttpCancellationTokenAccessor(accessorMock);

            // Act
            var resolvedToken = sut.ResolveCancellationToken(default);

            // Assert
            await Assert.That(resolvedToken).IsEqualTo(CancellationToken.None);
            await Assert.That(resolvedToken.CanBeCanceled).IsFalse();
        }
        #endregion
    }
}