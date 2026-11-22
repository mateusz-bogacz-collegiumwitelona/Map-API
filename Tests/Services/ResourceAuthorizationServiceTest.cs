using Domain.Const;
using Domain.Exceptions;
using Domain.Models;
using Infrastructure;
using Infrastructure.Interceptors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;
using Npgsql;
using Services.Interfaces;
using Services.Services;
using Testcontainers.PostgreSql;

namespace Tests.Services
{
    [NotInParallel]
    public class ResourceAuthorizationServiceTest
    {
        private AppDbContext _context = null!;
        private static PostgreSqlContainer _dbContainer = null!;
        private static string _connectionString = null!;
        private string _currentSchema = null!;

        private IResourceAuthorizationService _sut = null!;

        [Before(Class)]
        public static async Task SetupClassAsync()
        {
            _dbContainer = new PostgreSqlBuilder("postgis/postgis:18-3.6")
                .WithDatabase("testdb")
                .WithUsername("testuser")
                .WithPassword("testpassword")
                .Build();

            await _dbContainer.StartAsync();
            _connectionString = _dbContainer.GetConnectionString();
        }

        [After(Class)]
        public static async Task CleanupClassAsync()
            => await _dbContainer.DisposeAsync();

        [Before(Test)]
        public async Task SetupAsync()
        {
            _currentSchema = "auth_schema_" + Guid.NewGuid().ToString("N");

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
                .Options;

            _context = new AppDbContext(dbOptions);

            var createScript = _context.Database.GenerateCreateScript();
            await _context.Database.ExecuteSqlRawAsync(createScript);

            _sut = new ResourceAuthorizationService(_context);
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

        private async Task<User> AddUserAsync(bool isPublic)
        {
            var user = new User
            {
                Id = Guid.NewGuid(),
                FirstName = "Jan",
                LastName = "Kowalski",
                IsPublic = isPublic
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return user;
        }

        private async Task<Trip> AddTripAsync(User owner)
        {
            var trip = new Trip
            {
                UserId = owner.Id,
                Title = "Trip",
                StartDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            };

            _context.Trips.Add(trip);
            await _context.SaveChangesAsync();

            return trip;
        }

        private async Task<TripSegment> AddSegmentAsync(Trip trip)
        {
            var country = new Country
            {
                Name = "Poland",
                Code = "PL",
                Boundary = new MultiPolygon(new[]
                {
                    new Polygon(new LinearRing(new[]
                    {
                        new Coordinate(0, 0),
                        new Coordinate(0, 1),
                        new Coordinate(1, 1),
                        new Coordinate(0, 0)
                    }))
                })
                { SRID = GeoConst.SRID }
            };

            var origin = new Place
            {
                Name = "Krakow",
                Country = country,
                Location = new Point(19.94, 50.06) { SRID = GeoConst.SRID }
            };

            var destination = new Place
            {
                Name = "Rome",
                Country = country,
                Location = new Point(12.49, 41.90) { SRID = GeoConst.SRID }
            };

            var segment = new TripSegment
            {
                TripId = trip.Id,
                OrderIndex = 0,
                TransportType = Domain.Enum.TransportTypeEnum.Car,
                OriginPlace = origin,
                DestinationPlace = destination,
                DistanceKm = 100m,
                Geometry = new LineString(new[]
                {
                    origin.Location.Coordinate,
                    destination.Location.Coordinate
                })
                { SRID = GeoConst.SRID }
            };

            _context.TripSegments.Add(segment);
            await _context.SaveChangesAsync();

            return segment;
        }

        #region Read access

        [Test]
        public async Task EnsureCanReadProfileAsync_WhenOwnerProfileIsPrivate_ShouldAllowRead()
        {
            // Arrange
            var owner = await AddUserAsync(isPublic: false);

            // Act
            var resolvedId = await _sut.EnsureCanReadProfileAsync(owner.Id);

            // Assert
            await Assert.That(resolvedId).IsEqualTo(owner.Id);
        }

        [Test]
        public async Task EnsureCanReadProfileAsync_WhenOwnerProfileIsPublic_ShouldAllowRead()
        {
            // Arrange
            var owner = await AddUserAsync(isPublic: true);

            // Act
            var resolvedId = await _sut.EnsureCanReadProfileAsync(owner.Id, owner.Id);

            // Assert
            await Assert.That(resolvedId).IsEqualTo(owner.Id);
        }

        [Test]
        public async Task EnsureCanReadProfileAsync_WhenTargetProfileIsPublic_ShouldAllowThirdPartyRead()
        {
            // Arrange
            var requester = await AddUserAsync(isPublic: false);
            var target = await AddUserAsync(isPublic: true);

            // Act
            var resolvedId = await _sut.EnsureCanReadProfileAsync(requester.Id, target.Id);

            // Assert
            await Assert.That(resolvedId).IsEqualTo(target.Id);
        }

        [Test]
        public async Task EnsureCanReadProfileAsync_WhenTargetProfileIsPrivate_ShouldThrowNotFoundException()
        {
            // Arrange
            var requester = await AddUserAsync(isPublic: false);
            var target = await AddUserAsync(isPublic: false);

            // Act & Assert
            await Assert.That(async () => await _sut.EnsureCanReadProfileAsync(requester.Id, target.Id))
                .Throws<NotFoundException>()
                .WithMessage($"User with ID '{target.Id}' was not found.");
        }

        [Test]
        public async Task EnsureCanReadProfileAsync_WhenTargetDoesNotExist_ShouldThrowNotFoundException()
        {
            // Arrange
            var requester = await AddUserAsync(isPublic: false);
            var missingId = Guid.NewGuid();

            // Act & Assert
            await Assert.That(async () => await _sut.EnsureCanReadProfileAsync(requester.Id, missingId))
                .Throws<NotFoundException>()
                .WithMessage($"User with ID '{missingId}' was not found.");
        }

        [Test]
        public async Task EnsureCanReadProfileAsync_WhenTargetAccountIsDeleted_ShouldThrowNotFoundException()
        {
            // Arrange
            var requester = await AddUserAsync(isPublic: false);
            var target = await AddUserAsync(isPublic: true);

            _context.Users.Remove(target);
            await _context.SaveChangesAsync();

            // Act & Assert
            await Assert.That(async () => await _sut.EnsureCanReadProfileAsync(requester.Id, target.Id))
                .Throws<NotFoundException>();
        }

        #endregion

        #region Write access

        [Test]
        public async Task EnsureCanModifyTripAsync_WhenCallerIsOwner_ShouldAllowWrite()
        {
            // Arrange
            var owner = await AddUserAsync(isPublic: false);
            var trip = await AddTripAsync(owner);

            // Act & Assert
            await _sut.EnsureCanModifyTripAsync(owner.Id, trip.Id);
        }

        [Test]
        [Arguments(true)]
        [Arguments(false)]
        public async Task EnsureCanModifyTripAsync_WhenCallerIsNotOwner_ShouldThrowForbiddenRegardlessOfVisibility(
            bool targetIsPublic)
        {
            // Arrange
            var requester = await AddUserAsync(isPublic: false);
            var owner = await AddUserAsync(targetIsPublic);
            var trip = await AddTripAsync(owner);

            // Act
            ForbiddenException? exception = null;
            try
            {
                await _sut.EnsureCanModifyTripAsync(requester.Id, trip.Id);
            }
            catch (ForbiddenException ex)
            {
                exception = ex;
            }

            // Assert
            await Assert.That(exception).IsNotNull();
            await Assert.That(exception!.StatusCode).IsEqualTo(403);
            await Assert.That(exception.ErrorCode).IsEqualTo(ErrorCodes.AccessDenied);
            await Assert.That(exception.Message)
                .IsEqualTo($"Trip with ID '{trip.Id}' belongs to another user.");
        }

        [Test]
        public async Task EnsureCanModifyTripAsync_WhenTripDoesNotExist_ShouldThrowNotFoundException()
        {
            // Arrange
            var caller = await AddUserAsync(isPublic: true);
            var missingId = Guid.NewGuid();

            // Act & Assert
            await Assert.That(async () => await _sut.EnsureCanModifyTripAsync(caller.Id, missingId))
                .Throws<NotFoundException>()
                .WithMessage($"Trip with ID '{missingId}' was not found.");
        }

        [Test]
        public async Task EnsureCanModifyTripSegmentAsync_WhenCallerIsOwner_ShouldAllowWrite()
        {
            // Arrange
            var owner = await AddUserAsync(isPublic: false);
            var trip = await AddTripAsync(owner);
            var segment = await AddSegmentAsync(trip);

            // Act & Assert
            await _sut.EnsureCanModifyTripSegmentAsync(owner.Id, segment.Id);
        }

        [Test]
        [Arguments(true)]
        [Arguments(false)]
        public async Task EnsureCanModifyTripSegmentAsync_WhenCallerIsNotOwner_ShouldThrowForbiddenRegardlessOfVisibility(
            bool targetIsPublic)
        {
            // Arrange
            var requester = await AddUserAsync(isPublic: false);
            var owner = await AddUserAsync(targetIsPublic);
            var trip = await AddTripAsync(owner);
            var segment = await AddSegmentAsync(trip);

            // Act
            ForbiddenException? exception = null;
            try
            {
                await _sut.EnsureCanModifyTripSegmentAsync(requester.Id, segment.Id);
            }
            catch (ForbiddenException ex)
            {
                exception = ex;
            }

            // Assert
            await Assert.That(exception).IsNotNull();
            await Assert.That(exception!.StatusCode).IsEqualTo(403);
            await Assert.That(exception.ErrorCode).IsEqualTo(ErrorCodes.AccessDenied);
            await Assert.That(exception.Message)
                .IsEqualTo($"Trip segment with ID '{segment.Id}' belongs to another user.");
        }

        [Test]
        public async Task EnsureCanModifyTripSegmentAsync_WhenSegmentDoesNotExist_ShouldThrowNotFoundException()
        {
            // Arrange
            var caller = await AddUserAsync(isPublic: true);
            var missingId = Guid.NewGuid();

            // Act & Assert
            await Assert.That(async () => await _sut.EnsureCanModifyTripSegmentAsync(caller.Id, missingId))
                .Throws<NotFoundException>()
                .WithMessage($"Trip segment with ID '{missingId}' was not found.");
        }

        [Test]
        public void EnsureCanModify_WhenCallerIsOwner_ShouldAllowWrite()
        {
            // Arrange
            var userId = Guid.NewGuid();

            // Act & Assert
            _sut.EnsureCanModify(userId, userId, "Trip", Guid.NewGuid());
        }

        [Test]
        public async Task EnsureCanModify_WhenCallerIsNotOwner_ShouldThrowForbidden()
        {
            // Arrange
            var callerId = Guid.NewGuid();
            var ownerId = Guid.NewGuid();
            var resourceId = Guid.NewGuid();

            // Act
            ForbiddenException? exception = null;
            try
            {
                _sut.EnsureCanModify(callerId, ownerId, "Place", resourceId);
            }
            catch (ForbiddenException ex)
            {
                exception = ex;
            }

            // Assert
            await Assert.That(exception).IsNotNull();
            await Assert.That(exception!.StatusCode).IsEqualTo(403);
            await Assert.That(exception.ErrorCode).IsEqualTo(ErrorCodes.AccessDenied);
        }

        #endregion
    }
}
