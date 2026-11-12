using Domain.Const;
using Domain.Enum;
using Domain.Exceptions;
using Domain.Models;
using Infrastructure;
using Infrastructure.Interceptors;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;
using Npgsql;
using Services.Command.List;
using Services.Interfaces;
using Services.Response.Trip;
using Services.Services;
using System.Text.Json;
using Testcontainers.PostgreSql;
using Tests.Fakes;

namespace Tests.Services
{
    public class TripServicesTest
    {
        #region Configuration and Setup
        private AppDbContext _context = null!;
        private static PostgreSqlContainer _dbContainer = null!;
        private static string _connectionString = null!;

        private ITripServices _sut = null!;
        private string _currentSchema = null!;
        private Place _krakow = null!;
        private Place _rome = null!;

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

            var country = new Country
            {
                Name = "Test",
                Code = "TS",
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

            _krakow = new Place { Name = "Krakow", Country = country, Location = new Point(19.94, 50.06) { SRID = GeoConst.SRID } };
            _rome = new Place { Name = "Rome", Country = country, Location = new Point(12.49, 41.90) { SRID = GeoConst.SRID } };

            _context.Places.AddRange(_krakow, _rome);
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

        private async Task<Trip> AddTripAsync(
    User user,
    DateTime startDate,
    string title = "Trip",
    string? description = null,
    DateTime? endDate = null)
        {
            var trip = new Trip
            {
                UserId = user.Id,
                Title = title,
                Description = description,
                StartDate = startDate,
                EndDate = endDate
            };

            _context.Trips.Add(trip);
            await _context.SaveChangesAsync();

            return trip;
        }

        private async Task<TripSegment> AddSegmentAsync(
            Trip trip,
            int orderIndex,
            TransportTypeEnum transportType = TransportTypeEnum.Car,
            bool isFallbackRoute = false,
            Coordinate[]? route = null,
            decimal distanceKm = 1096.50m)
        {
            var segment = new TripSegment
            {
                TripId = trip.Id,
                OrderIndex = orderIndex,
                TransportType = transportType,
                OriginPlaceId = _krakow.Id,
                DestinationPlaceId = _rome.Id,
                DistanceKm = distanceKm,
                IsFallbackRoute = isFallbackRoute,
                Geometry = new LineString(route ?? new[]
                {
            _krakow.Location.Coordinate,
            new Coordinate(16.00, 46.00),
            _rome.Location.Coordinate
        })
                { SRID = GeoConst.SRID }
            };

            _context.TripSegments.Add(segment);
            await _context.SaveChangesAsync();

            return segment;
        }

        private static DateTime Utc(int year, int month, int day, int hour = 0, int minute = 0)
            => new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Utc);

        private static GeoCoordinate Point(double latitude, double longitude)
            => new GeoCoordinate { Latitude = latitude, Longitude = longitude };
        #endregion

        #region Response content
        [Test]
        public async Task GetTripRoutesAsync_ShouldReturnTripAndSegmentDetails()
        {
            // Arrange
            var user = await AddUserAsync();
            var trip = await AddTripAsync(user, Utc(2025, 5, 1), "Italy");
            trip.EndDate = Utc(2025, 5, 10);
            await AddSegmentAsync(trip, 0, TransportTypeEnum.Plane, isFallbackRoute: true);

            // Act
            var result = await _sut.GetTripRoutesAsync(user.Id);

            // Assert
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(result.Data!.Count).IsEqualTo(1);

            var route = result.Data[0];
            await Assert.That(route.TripId).IsEqualTo(trip.Id);
            await Assert.That(route.Title).IsEqualTo("Italy");
            await Assert.That(route.StartDate).IsEqualTo(Utc(2025, 5, 1));
            await Assert.That(route.EndDate).IsEqualTo(Utc(2025, 5, 10));

            var segment = route.Segments.Single();
            await Assert.That(segment.TransportType).IsEqualTo(TransportTypeEnum.Plane);
            await Assert.That(segment.DistanceKm).IsEqualTo(1096.50m);
            await Assert.That(segment.IsFallbackRoute).IsTrue();
            await Assert.That(segment.OriginName).IsEqualTo("Krakow");
            await Assert.That(segment.DestinationName).IsEqualTo("Rome");
        }

        [Test]
        public async Task GetTripRoutesAsync_ShouldReturnCoordinatesWithLatitudeAndLongitudeInRouteOrder()
        {
            // Arrange
            var user = await AddUserAsync();
            var trip = await AddTripAsync(user, Utc(2025, 5, 1));
            await AddSegmentAsync(trip, 0);

            // Act
            var result = await _sut.GetTripRoutesAsync(user.Id);

            // Assert
            var coordinates = result.Data![0].Segments[0].Coordinates;
            await Assert.That(coordinates.Count).IsEqualTo(3);
            await Assert.That(coordinates[0]).IsEqualTo(Point(50.06, 19.94));
            await Assert.That(coordinates[1]).IsEqualTo(Point(46.00, 16.00));
            await Assert.That(coordinates[2]).IsEqualTo(Point(41.90, 12.49));
        }

        [Test]
        public async Task GetTripRoutesAsync_ShouldDropPointsWithinToleranceAndKeepRouteShape()
        {
            // Arrange
            var user = await AddUserAsync();
            var trip = await AddTripAsync(user, Utc(2025, 5, 1));
            await AddSegmentAsync(trip, 0, route: new[]
            {
                new Coordinate(10.0000, 50.0000),
                new Coordinate(10.1000, 50.0001),
                new Coordinate(10.2000, 50.0000),
                new Coordinate(10.3000, 51.0000),
                new Coordinate(10.4000, 50.0000)
            });

            // Act
            var result = await _sut.GetTripRoutesAsync(user.Id);

            // Assert
            var coordinates = result.Data![0].Segments[0].Coordinates;
            await Assert.That(coordinates.Count).IsEqualTo(4);
            await Assert.That(coordinates[0]).IsEqualTo(Point(50.0000, 10.0000));
            await Assert.That(coordinates[1]).IsEqualTo(Point(50.0000, 10.2000));
            await Assert.That(coordinates[2]).IsEqualTo(Point(51.0000, 10.3000));
            await Assert.That(coordinates[3]).IsEqualTo(Point(50.0000, 10.4000));
        }

        [Test]
        public async Task GetTripRoutesAsync_WhenWholeRouteIsWithinTolerance_ShouldKeepEndpoints()
        {
            // Arrange
            var user = await AddUserAsync();
            var trip = await AddTripAsync(user, Utc(2025, 5, 1));
            await AddSegmentAsync(trip, 0, route: new[]
            {
                new Coordinate(10.0000, 50.0000),
                new Coordinate(10.0001, 50.0001),
                new Coordinate(10.0002, 50.0000)
            });

            // Act
            var result = await _sut.GetTripRoutesAsync(user.Id);

            // Assert
            var coordinates = result.Data![0].Segments[0].Coordinates;
            await Assert.That(coordinates.Count).IsEqualTo(2);
            await Assert.That(coordinates[0]).IsEqualTo(Point(50.0000, 10.0000));
            await Assert.That(coordinates[1]).IsEqualTo(Point(50.0000, 10.0002));
        }

        [Test]
        public async Task GetTripRoutesAsync_WhenRouteReturnsToStartWithinTolerance_ShouldStillReturnRoute()
        {
            // Arrange
            var user = await AddUserAsync();
            var trip = await AddTripAsync(user, Utc(2025, 5, 1));
            await AddSegmentAsync(trip, 0, route: new[]
            {
                new Coordinate(10.0000, 50.0000),
                new Coordinate(10.0001, 50.0001),
                new Coordinate(10.0000, 50.0000)
            });

            // Act
            var result = await _sut.GetTripRoutesAsync(user.Id);

            // Assert
            var coordinates = result.Data![0].Segments[0].Coordinates;
            await Assert.That(coordinates.Count).IsGreaterThanOrEqualTo(2);
            await Assert.That(coordinates[0]).IsEqualTo(Point(50.0000, 10.0000));
            await Assert.That(coordinates[^1]).IsEqualTo(Point(50.0000, 10.0000));
        }

        [Test]
        public async Task TripRouteResponse_ShouldSerializeTransportTypeAsText()
        {
            // Arrange
            var user = await AddUserAsync();
            var trip = await AddTripAsync(user, Utc(2025, 5, 1));
            await AddSegmentAsync(trip, 0, TransportTypeEnum.Plane);

            // Act
            var result = await _sut.GetTripRoutesAsync(user.Id);
            var json = JsonSerializer.Serialize(result.Data![0].Segments[0], new JsonSerializerOptions(JsonSerializerDefaults.Web));

            // Assert
            await Assert.That(json).Contains("\"transportType\":\"Plane\"");
            await Assert.That(json).Contains("\"coordinates\":[{\"latitude\":50.06,\"longitude\":19.94},{\"latitude\":46,\"longitude\":16},{\"latitude\":41.9,\"longitude\":12.49}]");
        }

        [Test]
        public async Task GetTripRoutesAsync_ShouldOrderTripsNewestFirstAndSegmentsByOrderIndex()
        {
            // Arrange
            var user = await AddUserAsync();
            var older = await AddTripAsync(user, Utc(2023, 1, 1), "Older");
            var newer = await AddTripAsync(user, Utc(2025, 1, 1), "Newer");
            await AddSegmentAsync(newer, 2);
            await AddSegmentAsync(newer, 0);
            await AddSegmentAsync(newer, 1);

            // Act
            var result = await _sut.GetTripRoutesAsync(user.Id);

            // Assert
            var routes = result.Data!;
            await Assert.That(routes.Select(t => t.TripId).SequenceEqual(new[] { newer.Id, older.Id })).IsTrue();
            await Assert.That(routes[0].Segments.Select(s => s.OrderIndex).SequenceEqual(new[] { 0, 1, 2 })).IsTrue();
        }

        [Test]
        public async Task GetTripRoutesAsync_WhenUserHasNoTrips_ShouldReturnEmptyList()
        {
            // Arrange
            var user = await AddUserAsync();

            // Act
            var result = await _sut.GetTripRoutesAsync(user.Id);

            // Assert
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(result.Data!).IsEmpty();
        }

        [Test]
        public async Task GetTripRoutesAsync_ShouldNotReturnTripsOfOtherUsers()
        {
            // Arrange
            var user = await AddUserAsync();
            var otherUser = await AddUserAsync();
            await AddTripAsync(otherUser, Utc(2025, 1, 1));

            // Act
            var result = await _sut.GetTripRoutesAsync(user.Id);

            // Assert
            await Assert.That(result.Data!).IsEmpty();
        }

        [Test]
        public async Task GetTripRoutesAsync_ShouldExcludeSoftDeletedTripsAndSegments()
        {
            // Arrange
            var user = await AddUserAsync();
            var deletedTrip = await AddTripAsync(user, Utc(2025, 1, 1), "Deleted");
            var activeTrip = await AddTripAsync(user, Utc(2024, 1, 1), "Active");
            await AddSegmentAsync(activeTrip, 0);
            var deletedSegment = await AddSegmentAsync(activeTrip, 1);

            _context.Remove(deletedTrip);
            _context.Remove(deletedSegment);
            await _context.SaveChangesAsync();

            // Act
            var result = await _sut.GetTripRoutesAsync(user.Id);

            // Assert
            var trip = result.Data!.Single();
            await Assert.That(trip.TripId).IsEqualTo(activeTrip.Id);
            await Assert.That(trip.Segments.Single().OrderIndex).IsEqualTo(0);
        }
        #endregion

        #region Year filter
        [Test]
        public async Task GetTripRoutesAsync_WhenYearIsNull_ShouldReturnAllYears()
        {
            // Arrange
            var user = await AddUserAsync();
            await AddTripAsync(user, Utc(2023, 6, 1));
            await AddTripAsync(user, Utc(2025, 6, 1));

            // Act
            var result = await _sut.GetTripRoutesAsync(user.Id);

            // Assert
            await Assert.That(result.Data!.Count).IsEqualTo(2);
        }

        [Test]
        public async Task GetTripRoutesAsync_WhenYearIsSet_ShouldReturnOnlyTripsStartedThatYear()
        {
            // Arrange
            var user = await AddUserAsync();
            await AddTripAsync(user, Utc(2024, 12, 31, 23, 59), "Last minute of 2024");
            var first = await AddTripAsync(user, Utc(2025, 1, 1), "First minute of 2025");
            var last = await AddTripAsync(user, Utc(2025, 12, 31, 23, 59), "Last minute of 2025");
            await AddTripAsync(user, Utc(2026, 1, 1), "First minute of 2026");

            // Act
            var result = await _sut.GetTripRoutesAsync(user.Id, year: 2025);

            // Assert
            await Assert.That(result.Data!.Select(t => t.TripId).SequenceEqual(new[] { last.Id, first.Id })).IsTrue();
        }

        [Test]
        public async Task GetTripRoutesAsync_WhenTripEndsInNextYear_ShouldMatchOnlyStartYear()
        {
            // Arrange
            var user = await AddUserAsync();
            var trip = await AddTripAsync(user, Utc(2024, 12, 20));
            trip.EndDate = Utc(2025, 1, 5);
            await _context.SaveChangesAsync();

            // Act
            var startYear = await _sut.GetTripRoutesAsync(user.Id, year: 2024);
            var endYear = await _sut.GetTripRoutesAsync(user.Id, year: 2025);

            // Assert
            await Assert.That(startYear.Data!.Count).IsEqualTo(1);
            await Assert.That(endYear.Data!).IsEmpty();
        }

        [Test]
        [Arguments(0)]
        [Arguments(-1)]
        [Arguments(99999)]
        public async Task GetTripRoutesAsync_WhenYearIsOutOfRange_ShouldReturnEmptyList(int year)
        {
            // Arrange
            var user = await AddUserAsync();
            await AddTripAsync(user, Utc(2025, 1, 1));

            // Act
            var result = await _sut.GetTripRoutesAsync(user.Id, year: year);

            // Assert
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(result.Data!).IsEmpty();
        }
        #endregion

        #region Privacy
        [Test]
        public async Task GetTripRoutesAsync_WhenTargetIsNull_ShouldReturnRoutesOfCurrentUser()
        {
            // Arrange
            var user = await AddUserAsync();
            var trip = await AddTripAsync(user, Utc(2025, 1, 1));

            // Act
            var result = await _sut.GetTripRoutesAsync(user.Id, targetUserId: null);

            // Assert
            await Assert.That(result.Data!.Single().TripId).IsEqualTo(trip.Id);
        }

        [Test]
        public async Task GetTripRoutesAsync_WhenOwnerHasPrivateProfile_ShouldReturnRoutes()
        {
            // Arrange
            var owner = await AddUserAsync(isPublic: false);
            await AddTripAsync(owner, Utc(2025, 1, 1));

            // Act
            var result = await _sut.GetTripRoutesAsync(owner.Id, owner.Id);

            // Assert
            await Assert.That(result.Data!.Count).IsEqualTo(1);
        }

        [Test]
        public async Task GetTripRoutesAsync_WhenTargetProfileIsPublic_ShouldReturnRoutes()
        {
            // Arrange
            var requester = await AddUserAsync();
            var target = await AddUserAsync(isPublic: true);
            var trip = await AddTripAsync(target, Utc(2025, 1, 1));

            // Act
            var result = await _sut.GetTripRoutesAsync(requester.Id, target.Id);

            // Assert
            await Assert.That(result.Data!.Single().TripId).IsEqualTo(trip.Id);
        }

        [Test]
        public async Task GetTripRoutesAsync_WhenTargetProfileIsPrivate_ShouldThrowNotFoundException()
        {
            // Arrange
            var requester = await AddUserAsync();
            var target = await AddUserAsync(isPublic: false);
            await AddTripAsync(target, Utc(2025, 1, 1));

            // Act & Assert
            await Assert.That(async () => await _sut.GetTripRoutesAsync(requester.Id, target.Id))
                .Throws<NotFoundException>()
                .WithMessage($"User with ID '{target.Id}' was not found.");
        }

        [Test]
        public async Task GetTripRoutesAsync_WhenTargetDoesNotExist_ShouldThrowNotFoundException()
        {
            // Arrange
            var requester = await AddUserAsync();
            var targetId = Guid.NewGuid();

            // Act & Assert
            await Assert.That(async () => await _sut.GetTripRoutesAsync(requester.Id, targetId))
                .Throws<NotFoundException>()
                .WithMessage($"User with ID '{targetId}' was not found.");
        }

        [Test]
        public async Task GetTripRoutesAsync_WhenTargetAccountIsDeleted_ShouldThrowNotFoundException()
        {
            // Arrange
            var requester = await AddUserAsync();
            var target = await AddUserAsync(isPublic: true);
            await AddTripAsync(target, Utc(2025, 1, 1));

            _context.Remove(target);
            await _context.SaveChangesAsync();

            // Act & Assert
            await Assert.That(async () => await _sut.GetTripRoutesAsync(requester.Id, target.Id))
                .Throws<NotFoundException>();
        }
        #endregion

        #region GetUserTripsAsync
        [Test]
        public async Task GetUserTripsAsync_ShouldReturnAggregatedTripSummary()
        {
            // Arrange
            var user = await AddUserAsync();
            var trip = await AddTripAsync(
                user,
                Utc(2025, 5, 1),
                title: "Eurotrip",
                description: "Short description",
                endDate: Utc(2025, 5, 12));

            await AddSegmentAsync(trip, 0, TransportTypeEnum.Plane, distanceKm: 1200.50m);
            await AddSegmentAsync(trip, 1, TransportTypeEnum.Train, distanceKm: 300.25m);
            await AddSegmentAsync(trip, 2, TransportTypeEnum.Plane, distanceKm: 850.00m);

            // Act
            var result = await _sut.GetUserTripsAsync(new BasicListCommand(), user.Id);

            // Assert
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(result.Data!.TotalCount).IsEqualTo(1);

            var item = result.Data.Items.Single();
            await Assert.That(item.Id).IsEqualTo(trip.Id);
            await Assert.That(item.Title).IsEqualTo("Eurotrip");
            await Assert.That(item.Description).IsEqualTo("Short description");
            await Assert.That(item.StartDate).IsEqualTo(Utc(2025, 5, 1));
            await Assert.That(item.EndDate).IsEqualTo(Utc(2025, 5, 12));
            await Assert.That(item.TotalDistance).IsEqualTo(2350.75m);
            await Assert.That(item.TransportTypes.Count).IsEqualTo(2);
            await Assert.That(item.TransportTypes).Contains(TransportTypeEnum.Plane);
            await Assert.That(item.TransportTypes).Contains(TransportTypeEnum.Train);
        }

        [Test]
        public async Task GetUserTripsAsync_WhenTripHasNoSegments_ShouldReturnZeroDistanceAndEmptyTransportTypes()
        {
            // Arrange
            var user = await AddUserAsync();
            var trip = await AddTripAsync(user, Utc(2025, 6, 1), "Planned Trip");

            // Act
            var result = await _sut.GetUserTripsAsync(new BasicListCommand(), user.Id);

            // Assert
            var item = result.Data!.Items.Single();
            await Assert.That(item.Id).IsEqualTo(trip.Id);
            await Assert.That(item.TotalDistance).IsEqualTo(0m);
            await Assert.That(item.TransportTypes).IsEmpty();
        }

        [Test]
        public async Task GetUserTripsAsync_WhenDescriptionExceeds100Characters_ShouldTruncateWithEllipsis()
        {
            // Arrange
            var user = await AddUserAsync();
            var exact100 = new string('a', 100);
            var over100 = new string('b', 105);

            var exactTrip = await AddTripAsync(user, Utc(2025, 5, 1), "Exact", description: exact100);
            var longTrip = await AddTripAsync(user, Utc(2025, 5, 2), "Long", description: over100);

            // Act
            var result = await _sut.GetUserTripsAsync(new BasicListCommand(), user.Id);

            // Assert
            var items = result.Data!.Items;
            var longItem = items.Single(x => x.Id == longTrip.Id);
            var exactItem = items.Single(x => x.Id == exactTrip.Id);

            await Assert.That(exactItem.Description).IsEqualTo(exact100);
            await Assert.That(longItem.Description).IsEqualTo(new string('b', 100) + "...");
        }

        [Test]
        public async Task GetUserTripsAsync_ShouldExcludeOtherUsersAndSoftDeletedTripsAndSegments()
        {
            // Arrange
            var user = await AddUserAsync();
            var otherUser = await AddUserAsync();

            await AddTripAsync(otherUser, Utc(2025, 7, 1), "Other User Trip");
            var deletedTrip = await AddTripAsync(user, Utc(2025, 6, 1), "Deleted Trip");
            var activeTrip = await AddTripAsync(user, Utc(2025, 5, 1), "Active Trip");

            await AddSegmentAsync(activeTrip, 0, TransportTypeEnum.Car, distanceKm: 250m);
            var deletedSegment = await AddSegmentAsync(activeTrip, 1, TransportTypeEnum.Plane, distanceKm: 900m);

            _context.Remove(deletedTrip);
            _context.Remove(deletedSegment);
            await _context.SaveChangesAsync();

            // Act
            var result = await _sut.GetUserTripsAsync(new BasicListCommand(), user.Id);

            // Assert
            await Assert.That(result.Data!.TotalCount).IsEqualTo(1);
            var item = result.Data.Items.Single();
            await Assert.That(item.Id).IsEqualTo(activeTrip.Id);
            await Assert.That(item.TotalDistance).IsEqualTo(250m);
            await Assert.That(item.TransportTypes.Single()).IsEqualTo(TransportTypeEnum.Car);
        }

        [Test]
        public async Task GetUserTripsAsync_ShouldOrderByStartDateDescendingByDefault()
        {
            // Arrange
            var user = await AddUserAsync();
            var oldest = await AddTripAsync(user, Utc(2023, 1, 1), "Oldest");
            var middle = await AddTripAsync(user, Utc(2024, 1, 1), "Middle");
            var newest = await AddTripAsync(user, Utc(2025, 1, 1), "Newest");

            // Act
            var result = await _sut.GetUserTripsAsync(new BasicListCommand(), user.Id);

            // Assert
            var ids = result.Data!.Items.Select(x => x.Id).ToList();
            await Assert.That(ids.SequenceEqual(new[] { newest.Id, middle.Id, oldest.Id })).IsTrue();
        }

        [Test]
        public async Task GetUserTripsAsync_WhenSortDescendingIsFalse_ShouldOrderChronologicallyAscending()
        {
            // Arrange
            var user = await AddUserAsync();
            var oldest = await AddTripAsync(user, Utc(2023, 1, 1), "Oldest");
            var newest = await AddTripAsync(user, Utc(2025, 1, 1), "Newest");

            var command = new BasicListCommand
            {
                SortBy = "startDate",
                SortDescending = false
            };

            // Act
            var result = await _sut.GetUserTripsAsync(command, user.Id);

            // Assert
            var ids = result.Data!.Items.Select(x => x.Id).ToList();
            await Assert.That(ids.SequenceEqual(new[] { oldest.Id, newest.Id })).IsTrue();
        }

        [Test]
        public async Task GetUserTripsAsync_ShouldPaginateWithoutDuplicatesAcrossPagesEvenWithIdenticalStartDates()
        {
            // Arrange
            var user = await AddUserAsync();
            var sameDate = Utc(2025, 5, 1);
            var createdIds = new List<Guid>();

            for (int i = 0; i < 5; i++)
            {
                var trip = await AddTripAsync(user, sameDate, $"Trip {i}");
                createdIds.Add(trip.Id);
            }

            // Act
            var page1 = await _sut.GetUserTripsAsync(new BasicListCommand { PageNumber = 1, PageSize = 2 }, user.Id);
            var page2 = await _sut.GetUserTripsAsync(new BasicListCommand { PageNumber = 2, PageSize = 2 }, user.Id);
            var page3 = await _sut.GetUserTripsAsync(new BasicListCommand { PageNumber = 3, PageSize = 2 }, user.Id);
            var page4 = await _sut.GetUserTripsAsync(new BasicListCommand { PageNumber = 4, PageSize = 2 }, user.Id);

            // Assert
            await Assert.That(page1.Data!.TotalCount).IsEqualTo(5);
            await Assert.That(page1.Data.TotalPages).IsEqualTo(3);
            await Assert.That(page1.Data.HasPreviousPage).IsFalse();
            await Assert.That(page1.Data.HasNextPage).IsTrue();

            await Assert.That(page2.Data!.HasPreviousPage).IsTrue();
            await Assert.That(page2.Data.HasNextPage).IsTrue();

            await Assert.That(page3.Data!.Items.Count).IsEqualTo(1);
            await Assert.That(page3.Data.HasNextPage).IsFalse();

            await Assert.That(page4.Data!.PageNumber).IsEqualTo(3);
            await Assert.That(page4.Data.Items.Single().Id).IsEqualTo(page3.Data.Items.Single().Id);
            await Assert.That(page4.Data.HasNextPage).IsFalse();

            var allReturnedIds = page1.Data.Items
                .Concat(page2.Data.Items)
                .Concat(page3.Data.Items)
                .Select(x => x.Id)
                .ToList();

            await Assert.That(allReturnedIds.Count).IsEqualTo(5);
            await Assert.That(allReturnedIds.Distinct().Count()).IsEqualTo(5);
            await Assert.That(allReturnedIds.OrderBy(x => x).SequenceEqual(createdIds.OrderBy(x => x))).IsTrue();
        }

        [Test]
        public async Task GetUserTripsAsync_ShouldSearchCaseAndAccentInsensitivelyInTitleAndDescription()
        {
            // Arrange
            var user = await AddUserAsync();
            var matchingByTitle = await AddTripAsync(user, Utc(2025, 5, 1), "Podróż do Krakowa");
            var matchingByDescription = await AddTripAsync(user, Utc(2025, 5, 2), "Wakacje", description: "Zwiedzanie Łodzi i okolic");
            await AddTripAsync(user, Utc(2025, 5, 3), "Wyprawa do Rzymu", description: "Koloseum");

            // Act
            var titleSearch = await _sut.GetUserTripsAsync(new BasicListCommand { SearchTerm = "podroz krakow" }, user.Id);
            var descriptionSearch = await _sut.GetUserTripsAsync(new BasicListCommand { SearchTerm = "lodz" }, user.Id);

            // Assert
            await Assert.That(titleSearch.Data!.Items.Single().Id).IsEqualTo(matchingByTitle.Id);
            await Assert.That(descriptionSearch.Data!.Items.Single().Id).IsEqualTo(matchingByDescription.Id);
        }

        [Test]
        public async Task GetUserTripsAsync_WhenUserHasNoTrips_ShouldReturnEmptyPagedResult()
        {
            // Arrange
            var user = await AddUserAsync();

            // Act
            var result = await _sut.GetUserTripsAsync(new BasicListCommand { PageNumber = 1, PageSize = 10 }, user.Id);

            // Assert
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(result.Data!.Items).IsEmpty();
            await Assert.That(result.Data.TotalCount).IsEqualTo(0);
            await Assert.That(result.Data.TotalPages).IsEqualTo(0);
            await Assert.That(result.Data.HasNextPage).IsFalse();
            await Assert.That(result.Data.HasPreviousPage).IsFalse();
        }
        #endregion
    }
}
