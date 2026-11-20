using Domain.Const;
using Domain.Enum;
using Domain.Models;
using Infrastructure;
using Infrastructure.Interceptors;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;
using Npgsql;
using Services.Command.Trip;
using Services.Helpers;
using Services.Interfaces;
using Services.Services;
using Testcontainers.PostgreSql;
using Tests.Fakes;

namespace Tests.Services
{
    public class TripServicesSegmentRouteTest
    {
        #region Configuration and Setup
        private AppDbContext _context = null!;
        private static PostgreSqlContainer _dbContainer = null!;
        private static string _connectionString = null!;

        private ITripServices _sut = null!;
        private string _currentSchema = null!;
        private Country _poland = null!;
        private Country _italy = null!;
        private Country _fiji = null!;
        private Country _samoa = null!;
        private Place _krakow = null!;
        private Place _rome = null!;
        private Place _suva = null!;
        private Place _apia = null!;

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

            _poland = CreateCountry("Poland", "PL");
            _italy = CreateCountry("Italy", "IT");
            _fiji = CreateCountry("Fiji", "FJ");
            _samoa = CreateCountry("Samoa", "WS");

            _krakow = CreatePlace("Krakow", _poland, 19.94, 50.06);
            _rome = CreatePlace("Rome", _italy, 12.49, 41.90);
            _suva = CreatePlace("Suva", _fiji, 178.44, -18.14);
            _apia = CreatePlace("Apia", _samoa, -171.76, -13.83);

            _context.Places.AddRange(_krakow, _rome, _suva, _apia);
            await _context.SaveChangesAsync();

            _sut = new TripServices(
                _context,
                new LoggerFactory().CreateLogger<TripServices>(),
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

        private async Task<User> AddUserAsync(bool isPublic = false)
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

        private async Task<Trip> AddTripAsync(User user, string title = "Trip")
        {
            var trip = new Trip
            {
                UserId = user.Id,
                Title = title,
                StartDate = new DateTime(2025, 5, 1, 0, 0, 0, DateTimeKind.Utc)
            };

            _context.Trips.Add(trip);
            await _context.SaveChangesAsync();

            return trip;
        }

        private async Task<TripSegment> AddSegmentAsync(
            Trip trip,
            TransportTypeEnum transportType = TransportTypeEnum.Car,
            Place? origin = null,
            Place? destination = null)
        {
            var originPlace = origin ?? _krakow;
            var destinationPlace = destination ?? _rome;

            var segment = new TripSegment
            {
                TripId = trip.Id,
                OrderIndex = 0,
                TransportType = transportType,
                OriginPlaceId = originPlace.Id,
                DestinationPlaceId = destinationPlace.Id,
                DistanceKm = 1.00m,
                IsFallbackRoute = true,
                Geometry = new LineString(new[]
                {
                    originPlace.Location.Coordinate,
                    destinationPlace.Location.Coordinate
                })
                { SRID = GeoConst.SRID }
            };

            _context.TripSegments.Add(segment);
            await _context.SaveChangesAsync();

            return segment;
        }

        private async Task<TripSegment> ReloadSegmentAsync(Guid segmentId)
        {
            _context.ChangeTracker.Clear();

            return await _context.TripSegments
                .IgnoreQueryFilters()
                .AsNoTracking()
                .SingleAsync(s => s.Id == segmentId);
        }

        private static UpdateSegmentRouteCommand Command(decimal distanceKm, bool isFallbackRoute, params (double Latitude, double Longitude)[] points)
            => new()
            {
                Coordinates = points
                    .Select(p => new RoutePointCommand { Latitude = p.Latitude, Longitude = p.Longitude })
                    .ToList(),
                DistanceKm = distanceKm,
                IsFallbackRoute = isFallbackRoute
            };

        private static UpdateSegmentRouteCommand KrakowToRome(decimal distanceKm = 1250.75m, bool isFallbackRoute = false)
            => Command(distanceKm, isFallbackRoute, (50.06, 19.94), (46.00, 16.00), (41.90, 12.49));

        private static decimal RouteKm(params (double Latitude, double Longitude)[] points)
            => (decimal)points
                .Zip(points.Skip(1), (from, to) => GeoDistanceHelper.HaversineKm(from.Latitude, from.Longitude, to.Latitude, to.Longitude))
                .Sum();

        private static Country CreateCountry(string name, string code)
            => new Country
            {
                Name = name,
                Code = code,
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

        private static Place CreatePlace(string name, Country country, double longitude, double latitude)
            => new Place { Name = name, Country = country, Location = new Point(longitude, latitude) { SRID = GeoConst.SRID } };
        #endregion

        #region Storing routes
        [Test]
        public async Task UpdateSegmentRouteAsync_ShouldStoreRouteDistanceAndFallbackFlag()
        {
            // Arrange
            var user = await AddUserAsync();
            var trip = await AddTripAsync(user);
            var segment = await AddSegmentAsync(trip);

            // Act
            var result = await _sut.UpdateSegmentRouteAsync(user.Id, segment.Id, KrakowToRome(1250.75m, isFallbackRoute: false));

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);

            var stored = await ReloadSegmentAsync(segment.Id);
            await Assert.That(stored.DistanceKm).IsEqualTo(1250.75m);
            await Assert.That(stored.IsFallbackRoute).IsFalse();
            await Assert.That(stored.Geometry.SRID).IsEqualTo(GeoConst.SRID);
            await Assert.That(stored.Geometry.GeometryType).IsEqualTo("LineString");
            await Assert.That(stored.Geometry.Coordinates.Select(c => (c.Y, c.X)).SequenceEqual(new[] { (50.06, 19.94), (46.00, 16.00), (41.90, 12.49) })).IsTrue();
        }

        [Test]
        [Arguments(TransportTypeEnum.Car)]
        [Arguments(TransportTypeEnum.Plane)]
        [Arguments(TransportTypeEnum.Train)]
        [Arguments(TransportTypeEnum.Other)]
        public async Task UpdateSegmentRouteAsync_ShouldStoreRouteForEveryTransportType(TransportTypeEnum transportType)
        {
            // Arrange
            var user = await AddUserAsync();
            var trip = await AddTripAsync(user);
            var segment = await AddSegmentAsync(trip, transportType);

            // Act
            var result = await _sut.UpdateSegmentRouteAsync(user.Id, segment.Id, KrakowToRome(1073.42m, isFallbackRoute: true));

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();

            var stored = await ReloadSegmentAsync(segment.Id);
            await Assert.That(stored.TransportType).IsEqualTo(transportType);
            await Assert.That(stored.Geometry.NumPoints).IsEqualTo(3);
            await Assert.That(stored.IsFallbackRoute).IsTrue();
        }

        [Test]
        public async Task UpdateSegmentRouteAsync_ShouldReplaceThePreviousRoute()
        {
            // Arrange
            var user = await AddUserAsync();
            var trip = await AddTripAsync(user);
            var segment = await AddSegmentAsync(trip);
            await _sut.UpdateSegmentRouteAsync(user.Id, segment.Id, KrakowToRome(1250.75m));

            // Act
            var result = await _sut.UpdateSegmentRouteAsync(user.Id, segment.Id, Command(1073.35m, true, (50.06, 19.94), (41.90, 12.49)));

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();

            var stored = await ReloadSegmentAsync(segment.Id);
            await Assert.That(stored.Geometry.NumPoints).IsEqualTo(2);
            await Assert.That(stored.DistanceKm).IsEqualTo(1073.35m);
            await Assert.That(stored.IsFallbackRoute).IsTrue();
        }

        [Test]
        public async Task UpdateSegmentRouteAsync_ShouldBeReturnedByTripRoutes()
        {
            // Arrange
            var user = await AddUserAsync();
            var trip = await AddTripAsync(user);
            var segment = await AddSegmentAsync(trip, TransportTypeEnum.Train);
            await _sut.UpdateSegmentRouteAsync(user.Id, segment.Id, KrakowToRome(1250.75m));
            _context.ChangeTracker.Clear();

            // Act
            var result = await _sut.GetTripRoutesAsync(user.Id);

            // Assert
            var returned = result.Data!.Single().Segments.Single();
            await Assert.That(returned.SegmentId).IsEqualTo(segment.Id);
            await Assert.That(returned.DistanceKm).IsEqualTo(1250.75m);
            await Assert.That(returned.IsFallbackRoute).IsFalse();
            await Assert.That(returned.Coordinates.Select(c => (c.Latitude, c.Longitude)).SequenceEqual(new[] { (50.06, 19.94), (46.00, 16.00), (41.90, 12.49) })).IsTrue();
        }

        [Test]
        public async Task UpdateSegmentRouteAsync_WhenRouteCrossesTheAntimeridian_ShouldStorePointsUnchanged()
        {
            // Arrange
            var user = await AddUserAsync();
            var trip = await AddTripAsync(user);
            var segment = await AddSegmentAsync(trip, TransportTypeEnum.Plane, _suva, _apia);
            var points = new[] { (-18.14, 178.44), (-17.00, 179.90), (-16.50, -179.90), (-13.83, -171.76) };

            // Act
            var result = await _sut.UpdateSegmentRouteAsync(user.Id, segment.Id, Command(1200.00m, false, points));

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();

            var stored = await ReloadSegmentAsync(segment.Id);
            await Assert.That(stored.Geometry.Coordinates.Select(c => (c.Y, c.X)).SequenceEqual(points)).IsTrue();
        }
        #endregion

        #region Ownership
        [Test]
        public async Task UpdateSegmentRouteAsync_WhenSegmentBelongsToAnotherUser_ShouldReturnNotFoundAndKeepRoute()
        {
            // Arrange
            var owner = await AddUserAsync();
            var other = await AddUserAsync(isPublic: true);
            var trip = await AddTripAsync(owner);
            var segment = await AddSegmentAsync(trip);

            // Act
            var result = await _sut.UpdateSegmentRouteAsync(other.Id, segment.Id, KrakowToRome());

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status404NotFound);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.NotFound);
            await Assert.That(result.Message).IsEqualTo($"Trip segment with ID '{segment.Id}' was not found.");

            var stored = await ReloadSegmentAsync(segment.Id);
            await Assert.That(stored.Geometry.NumPoints).IsEqualTo(2);
            await Assert.That(stored.DistanceKm).IsEqualTo(1.00m);
        }

        [Test]
        public async Task UpdateSegmentRouteAsync_WhenSegmentDoesNotExist_ShouldReturnNotFound()
        {
            // Arrange
            var user = await AddUserAsync();
            var segmentId = Guid.NewGuid();

            // Act
            var result = await _sut.UpdateSegmentRouteAsync(user.Id, segmentId, KrakowToRome());

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status404NotFound);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.NotFound);
            await Assert.That(result.Message).IsEqualTo($"Trip segment with ID '{segmentId}' was not found.");
        }

        [Test]
        public async Task UpdateSegmentRouteAsync_WhenSegmentIsDeleted_ShouldReturnNotFound()
        {
            // Arrange
            var user = await AddUserAsync();
            var trip = await AddTripAsync(user);
            var segment = await AddSegmentAsync(trip);
            _context.TripSegments.Remove(segment);
            await _context.SaveChangesAsync();

            // Act
            var result = await _sut.UpdateSegmentRouteAsync(user.Id, segment.Id, KrakowToRome());

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status404NotFound);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.NotFound);
            await Assert.That(result.Message).IsEqualTo($"Trip segment with ID '{segment.Id}' was not found.");
        }

        [Test]
        public async Task UpdateSegmentRouteAsync_WhenTripIsDeleted_ShouldReturnNotFoundAndKeepRoute()
        {
            // Arrange
            var user = await AddUserAsync();
            var trip = await AddTripAsync(user);
            var segment = await AddSegmentAsync(trip);
            _context.Trips.Remove(trip);
            await _context.SaveChangesAsync();
            _context.ChangeTracker.Clear();

            // Act
            var result = await _sut.UpdateSegmentRouteAsync(user.Id, segment.Id, KrakowToRome());

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status404NotFound);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.NotFound);
            await Assert.That(result.Message).IsEqualTo($"Trip segment with ID '{segment.Id}' was not found.");

            var stored = await ReloadSegmentAsync(segment.Id);
            await Assert.That(stored.Geometry.NumPoints).IsEqualTo(2);
        }
        #endregion

        #region Route endpoints
        [Test]
        public async Task UpdateSegmentRouteAsync_WhenRouteStartsFarFromOrigin_ShouldReturnEndpointsMismatchAndKeepRoute()
        {
            // Arrange
            var user = await AddUserAsync();
            var trip = await AddTripAsync(user);
            var segment = await AddSegmentAsync(trip);

            // Act
            var result = await _sut.UpdateSegmentRouteAsync(user.Id, segment.Id, Command(1300.00m, false, (50.16, 19.94), (41.90, 12.49)));

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.RouteEndpointsMismatch);

            var stored = await ReloadSegmentAsync(segment.Id);
            await Assert.That(stored.Geometry.NumPoints).IsEqualTo(2);
            await Assert.That(stored.DistanceKm).IsEqualTo(1.00m);
        }

        [Test]
        public async Task UpdateSegmentRouteAsync_WhenRouteEndsFarFromDestination_ShouldReturnEndpointsMismatch()
        {
            // Arrange
            var user = await AddUserAsync();
            var trip = await AddTripAsync(user);
            var segment = await AddSegmentAsync(trip);

            // Act
            var result = await _sut.UpdateSegmentRouteAsync(user.Id, segment.Id, Command(1300.00m, false, (50.06, 19.94), (41.80, 12.49)));

            // Assert
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.RouteEndpointsMismatch);
        }

        [Test]
        public async Task UpdateSegmentRouteAsync_WhenRouteIsReversed_ShouldReturnEndpointsMismatch()
        {
            // Arrange
            var user = await AddUserAsync();
            var trip = await AddTripAsync(user);
            var segment = await AddSegmentAsync(trip);

            // Act
            var result = await _sut.UpdateSegmentRouteAsync(user.Id, segment.Id, Command(1300.00m, false, (41.90, 12.49), (50.06, 19.94)));

            // Assert
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.RouteEndpointsMismatch);
        }

        [Test]
        public async Task UpdateSegmentRouteAsync_WhenEndpointsAreWithinTolerance_ShouldStoreRoute()
        {
            // Arrange
            var user = await AddUserAsync();
            var trip = await AddTripAsync(user);
            var segment = await AddSegmentAsync(trip);

            // Act
            var result = await _sut.UpdateSegmentRouteAsync(user.Id, segment.Id, Command(1300.00m, false, (50.14, 19.94), (41.82, 12.49)));

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
        }
        #endregion

        #region Distance
        [Test]
        public async Task UpdateSegmentRouteAsync_WhenDistanceIsShorterThanRouteLength_ShouldReturnDistanceInvalidAndKeepRoute()
        {
            // Arrange
            var user = await AddUserAsync();
            var trip = await AddTripAsync(user);
            var segment = await AddSegmentAsync(trip);
            var tooShort = Math.Round(RouteKm((50.06, 19.94), (46.00, 16.00), (41.90, 12.49)) * 0.98m, 2);

            // Act
            var result = await _sut.UpdateSegmentRouteAsync(user.Id, segment.Id, KrakowToRome(tooShort));

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.DistanceInvalid);

            var stored = await ReloadSegmentAsync(segment.Id);
            await Assert.That(stored.DistanceKm).IsEqualTo(1.00m);
        }

        [Test]
        public async Task UpdateSegmentRouteAsync_WhenDistanceIsWithinToleranceOfRouteLength_ShouldStoreRoute()
        {
            // Arrange
            var user = await AddUserAsync();
            var trip = await AddTripAsync(user);
            var segment = await AddSegmentAsync(trip);
            var slightlyShort = Math.Round(RouteKm((50.06, 19.94), (46.00, 16.00), (41.90, 12.49)) * 0.995m, 2);

            // Act
            var result = await _sut.UpdateSegmentRouteAsync(user.Id, segment.Id, KrakowToRome(slightlyShort));

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();

            var stored = await ReloadSegmentAsync(segment.Id);
            await Assert.That(stored.DistanceKm).IsEqualTo(slightlyShort);
        }

        [Test]
        public async Task UpdateSegmentRouteAsync_WhenRouteLineIsLongerThanDistance_ShouldReturnDistanceInvalid()
        {
            // Arrange
            var user = await AddUserAsync();
            var trip = await AddTripAsync(user);
            var segment = await AddSegmentAsync(trip);
            var straightLine = Math.Round(RouteKm((50.06, 19.94), (41.90, 12.49)), 2);

            // Act
            var result = await _sut.UpdateSegmentRouteAsync(user.Id, segment.Id, Command(straightLine, false, (50.06, 19.94), (44.00, 28.00), (41.90, 12.49)));

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.DistanceInvalid);
        }

        [Test]
        public async Task UpdateSegmentRouteAsync_WhenShortDistanceIsRoundedDown_ShouldStoreRoute()
        {
            // Arrange
            var user = await AddUserAsync();
            var trip = await AddTripAsync(user);
            var segment = await AddSegmentAsync(trip, TransportTypeEnum.Car, _krakow, _krakow);
            var roundedDown = Math.Round(RouteKm((50.06, 19.94), (50.06273, 19.94)), 2);

            // Act
            var result = await _sut.UpdateSegmentRouteAsync(user.Id, segment.Id, Command(roundedDown, false, (50.06, 19.94), (50.06273, 19.94)));

            // Assert
            await Assert.That(roundedDown).IsEqualTo(0.30m);
            await Assert.That(result.IsSuccess).IsTrue();
        }

        [Test]
        public async Task UpdateSegmentRouteAsync_WhenDistanceIsMoreThanMaxRatioOfRouteLength_ShouldReturnDistanceInvalidAndKeepRoute()
        {
            // Arrange
            var user = await AddUserAsync();
            var trip = await AddTripAsync(user);
            var segment = await AddSegmentAsync(trip);
            var tooLong = Math.Round(RouteKm((50.06, 19.94), (46.00, 16.00), (41.90, 12.49)) * 1.51m, 2);

            // Act
            var result = await _sut.UpdateSegmentRouteAsync(user.Id, segment.Id, KrakowToRome(tooLong));

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.DistanceInvalid);

            var stored = await ReloadSegmentAsync(segment.Id);
            await Assert.That(stored.DistanceKm).IsEqualTo(1.00m);
        }

        [Test]
        public async Task UpdateSegmentRouteAsync_WhenDistanceIsWithinMaxRatioOfRouteLength_ShouldStoreRoute()
        {
            // Arrange
            var user = await AddUserAsync();
            var trip = await AddTripAsync(user);
            var segment = await AddSegmentAsync(trip);
            var longer = Math.Round(RouteKm((50.06, 19.94), (46.00, 16.00), (41.90, 12.49)) * 1.49m, 2);

            // Act
            var result = await _sut.UpdateSegmentRouteAsync(user.Id, segment.Id, KrakowToRome(longer));

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();

            var stored = await ReloadSegmentAsync(segment.Id);
            await Assert.That(stored.DistanceKm).IsEqualTo(longer);
        }

        [Test]
        public async Task UpdateSegmentRouteAsync_WhenFallbackDistanceIsLongerThanTheLine_ShouldReturnDistanceInvalid()
        {
            // Arrange
            var user = await AddUserAsync();
            var trip = await AddTripAsync(user);
            var segment = await AddSegmentAsync(trip);
            var longer = Math.Round(RouteKm((50.06, 19.94), (41.90, 12.49)) * 1.05m, 2);

            // Act
            var result = await _sut.UpdateSegmentRouteAsync(user.Id, segment.Id, Command(longer, true, (50.06, 19.94), (41.90, 12.49)));

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.DistanceInvalid);
        }

        [Test]
        public async Task UpdateSegmentRouteAsync_WhenFallbackDistanceMatchesTheLine_ShouldStoreRoute()
        {
            // Arrange
            var user = await AddUserAsync();
            var trip = await AddTripAsync(user);
            var segment = await AddSegmentAsync(trip);
            var line = Math.Round(RouteKm((50.06, 19.94), (41.90, 12.49)), 2);

            // Act
            var result = await _sut.UpdateSegmentRouteAsync(user.Id, segment.Id, Command(line, true, (50.06, 19.94), (41.90, 12.49)));

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
        }
        #endregion

        #region Route points
        [Test]
        public async Task UpdateSegmentRouteAsync_WhenRouteHasConsecutiveDuplicatePoints_ShouldStoreEachPointOnce()
        {
            // Arrange
            var user = await AddUserAsync();
            var trip = await AddTripAsync(user);
            var segment = await AddSegmentAsync(trip);

            // Act
            var result = await _sut.UpdateSegmentRouteAsync(user.Id, segment.Id, Command(1250.75m, false, (50.06, 19.94), (50.06, 19.94), (46.00, 16.00), (46.00, 16.00), (46.00, 16.00), (41.90, 12.49)));

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();

            var stored = await ReloadSegmentAsync(segment.Id);
            await Assert.That(stored.Geometry.Coordinates.Select(c => (c.Y, c.X)).SequenceEqual(new[] { (50.06, 19.94), (46.00, 16.00), (41.90, 12.49) })).IsTrue();
        }

        [Test]
        public async Task UpdateSegmentRouteAsync_WhenAllPointsAreTheSame_ShouldReturnRouteInvalidAndKeepRoute()
        {
            // Arrange
            var user = await AddUserAsync();
            var trip = await AddTripAsync(user);
            var segment = await AddSegmentAsync(trip, TransportTypeEnum.Car, _krakow, _krakow);

            // Act
            var result = await _sut.UpdateSegmentRouteAsync(user.Id, segment.Id, Command(0.01m, false, (50.06, 19.94), (50.06, 19.94), (50.06, 19.94)));

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.RouteInvalid);

            var stored = await ReloadSegmentAsync(segment.Id);
            await Assert.That(stored.DistanceKm).IsEqualTo(1.00m);
        }

        [Test]
        public async Task UpdateSegmentRouteAsync_WhenRouteHasOnePoint_ShouldReturnRouteInvalid()
        {
            // Arrange
            var user = await AddUserAsync();
            var trip = await AddTripAsync(user);
            var segment = await AddSegmentAsync(trip);

            // Act
            var result = await _sut.UpdateSegmentRouteAsync(user.Id, segment.Id, Command(1.00m, false, (50.06, 19.94)));

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.RouteInvalid);
        }

        [Test]
        public async Task UpdateSegmentRouteAsync_WhenCoordinatesAreNull_ShouldReturnRouteInvalid()
        {
            // Arrange
            var user = await AddUserAsync();
            var trip = await AddTripAsync(user);
            var segment = await AddSegmentAsync(trip);
            var command = new UpdateSegmentRouteCommand
            {
                Coordinates = null!,
                DistanceKm = 1.00m,
                IsFallbackRoute = false
            };

            // Act
            var result = await _sut.UpdateSegmentRouteAsync(user.Id, segment.Id, command);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.RouteInvalid);
        }

        [Test]
        public async Task UpdateSegmentRouteAsync_ShouldNotTrackSegmentPlaces()
        {
            // Arrange
            var user = await AddUserAsync();
            var trip = await AddTripAsync(user);
            var segment = await AddSegmentAsync(trip);
            _context.ChangeTracker.Clear();

            // Act
            var result = await _sut.UpdateSegmentRouteAsync(user.Id, segment.Id, KrakowToRome());

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(_context.ChangeTracker.Entries<Place>().Count()).IsEqualTo(0);
        }
        #endregion
    }
}
