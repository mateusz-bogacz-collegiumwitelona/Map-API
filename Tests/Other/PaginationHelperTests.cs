using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Services.Helpers;

namespace Tests.Other
{
    public class PaginationHelperTests
    {
        private PaginationTestDbContext _context = null!;
        private ILogger _logger = null!;


        [Before(Test)]
        public async Task SetupAsync()
        {
            var options = new DbContextOptionsBuilder<PaginationTestDbContext>()
                .UseInMemoryDatabase(databaseName: $"PaginationTestDb_{Guid.NewGuid():N}")
                .Options;

            _context = new PaginationTestDbContext(options);
            _logger = NullLogger.Instance;

            var testItems = Enumerable.Range(1, 25).Select(i => new TestItem
            {
                Id = i,
                Name = $"Item {i}"
            });

            await _context.Items.AddRangeAsync(testItems);
            await _context.SaveChangesAsync();
        }

        [After(Test)]
        public async Task TearDownAsync()
        {
            await _context.Database.EnsureDeletedAsync();
            await _context.DisposeAsync();
        }

        [Test]
        public async Task ToPagedResultAsync_WhenParametersAreNull_UsesDefaultPageOneAndSizeTen()
        {
            // Act
            var result = await _context.Items.ToPagedResultAsync(
                number: null,
                size: null,
                logger: _logger,
                entityName: "test items"
            );

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(result.Message).IsEqualTo("test items retrieved successfully.");

            var paged = result.Data;
            await Assert.That(paged).IsNotNull();
            await Assert.That(paged!.PageNumber).IsEqualTo(1);
            await Assert.That(paged.PageSize).IsEqualTo(10);
            await Assert.That(paged.TotalCount).IsEqualTo(25);
            await Assert.That(paged.TotalPages).IsEqualTo(3);
            await Assert.That(paged.Items.Count).IsEqualTo(10);
            await Assert.That(paged.HasPreviousPage).IsFalse();
            await Assert.That(paged.HasNextPage).IsTrue();
        }

        [Test]
        public async Task ToPagedResultAsync_WhenRequestingMiddlePage_ReturnsCorrectSliceAndNavigationFlags()
        {
            // Act
            var result = await _context.Items.ToPagedResultAsync(
                number: 2,
                size: 10,
                logger: _logger
            );

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            var paged = result.Data!;

            await Assert.That(paged.PageNumber).IsEqualTo(2);
            await Assert.That(paged.Items.Count).IsEqualTo(10);
            await Assert.That(paged.Items.First().Id).IsEqualTo(11);
            await Assert.That(paged.Items.Last().Id).IsEqualTo(20);
            await Assert.That(paged.HasPreviousPage).IsTrue();
            await Assert.That(paged.HasNextPage).IsTrue();
        }

        [Test]
        public async Task ToPagedResultAsync_WhenRequestingLastPage_ReturnsRemainingItemsAndDisablesNextPage()
        {
            // Act
            var result = await _context.Items.ToPagedResultAsync(
                number: 3,
                size: 10,
                logger: _logger
            );

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            var paged = result.Data!;

            await Assert.That(paged.PageNumber).IsEqualTo(3);
            await Assert.That(paged.Items.Count).IsEqualTo(5);
            await Assert.That(paged.HasPreviousPage).IsTrue();
            await Assert.That(paged.HasNextPage).IsFalse();
        }


        [Test]
        public async Task ToPagedResultAsync_WhenCollectionIsEmpty_ReturnsSuccessWithEmptyPagedResult()
        {
            // Arrange
            var emptyQuery = _context.Items.Where(x => x.Id > 1000);

            // Act
            var result = await emptyQuery.ToPagedResultAsync(
                number: 1,
                size: 10,
                logger: _logger,
                entityName: "products"
            );

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(result.Message).IsEqualTo("No products found.");

            var paged = result.Data!;
            await Assert.That(paged.Items).IsEmpty();
            await Assert.That(paged.TotalCount).IsEqualTo(0);
            await Assert.That(paged.TotalPages).IsEqualTo(0);
            await Assert.That(paged.HasPreviousPage).IsFalse();
            await Assert.That(paged.HasNextPage).IsFalse();
        }

        [Test]
        public async Task ToPagedResultAsync_WhenPageNumberExceedsTotalPages_ClampsToLastPage()
        {
            // Act
            var result = await _context.Items.ToPagedResultAsync(
                number: 99,
                size: 10,
                logger: _logger
            );

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            var paged = result.Data!;

            await Assert.That(paged.PageNumber).IsEqualTo(3);
            await Assert.That(paged.Items.Count).IsEqualTo(5);
            await Assert.That(paged.HasNextPage).IsFalse();
        }

        [Test]
        public async Task ToPagedResultAsync_WhenSourceIsNull_ReturnsFailure500()
        {
            // Arrange
            IQueryable<TestItem> nullSource = null!;

            // Act
            var result = await nullSource.ToPagedResultAsync(
                number: 1,
                size: 10,
                logger: _logger
            );

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status500InternalServerError);
            await Assert.That(result.Message).IsEqualTo("An error occurred while processing your request.");
            await Assert.That(result.Errors).IsNotNull();
            await Assert.That(result.Errors!.First()).Contains("Value cannot be null");
        }

        [Test]
        public async Task ToPagedResultAsync_WhenCancellationRequested_RethrowsOperationCanceledException()
        {
            // Arrange
            using var cts = new CancellationTokenSource();
            cts.Cancel(); 

            // Act & Assert
            await Assert.That(async () =>
            {
                await _context.Items.ToPagedResultAsync(
                    number: 1,
                    size: 10,
                    logger: _logger,
                    cancellationToken: cts.Token
                );
            }).Throws<OperationCanceledException>();
        }

        public class TestItem
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
        }

        private class PaginationTestDbContext : DbContext
        {
            public PaginationTestDbContext(DbContextOptions<PaginationTestDbContext> options)
                : base(options) { }

            public DbSet<TestItem> Items => Set<TestItem>();
        }
    }
}
