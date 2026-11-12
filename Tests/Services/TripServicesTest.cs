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
        private Country _poland = null!;
        private Country _italy = null!;
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

            _poland = CreateCountry("Poland", "PL");
            _italy = CreateCountry("Italy", "IT");

            _krakow = CreatePlace("Krakow", _poland, 19.94, 50.06);
            _rome = CreatePlace("Rome", _italy, 12.49, 41.90);

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

        private async Task<Trip> AddTripAsync(User user, DateTime startDate, string title = "Trip")
        {
            var trip = new Trip
            {
                UserId = user.Id,
                Title = title,
                StartDate = startDate
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
            Place? origin = null,
            Place? destination = null)
        {
            var segment = new TripSegment
            {
                TripId = trip.Id,
                OrderIndex = orderIndex,
                TransportType = transportType,
                OriginPlaceId = (origin ?? _krakow).Id,
                DestinationPlaceId = (destination ?? _rome).Id,
                DistanceKm = 1096.50m,
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

        private async Task<Place> AddPlaceAsync(string name, Country country)
        {
            var place = CreatePlace(name, country, 0, 0);

            _context.Places.Add(place);
            await _context.SaveChangesAsync();

            return place;
        }

        private async Task AddVisitAsync(Trip trip, Country country)
        {
            _context.VisitedCountries.Add(new VisitedCountry
            {
                UserId = trip.UserId,
                TripId = trip.Id,
                CountryId = country.Id,
                Year = trip.StartDate.Year
            });

            await _context.SaveChangesAsync();
        }

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

        #region Visited countries
        [Test]
        public async Task GetVisitedCountriesAsync_ShouldGroupPlacesAndTripsByCountryOrderedByName()
        {
            // Arrange
            var user = await AddUserAsync();
            var trip = await AddTripAsync(user, Utc(2025, 5, 1), "Italy trip");
            await AddSegmentAsync(trip, 0);
            await AddVisitAsync(trip, _poland);
            await AddVisitAsync(trip, _italy);

            // Act
            var result = await _sut.GetVisitedCountriesAsync(user.Id);

            // Assert
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            var countries = result.Data!;
            await Assert.That(countries.Select(c => c.CountryCode).SequenceEqual(new[] { "IT", "PL" })).IsTrue();

            var italy = countries[0];
            await Assert.That(italy.CountryName).IsEqualTo("Italy");
            await Assert.That(italy.PlaceNames.SequenceEqual(new[] { "Rome" })).IsTrue();
            await Assert.That(italy.Trips.Single().TripId).IsEqualTo(trip.Id);
            await Assert.That(italy.Trips.Single().Title).IsEqualTo("Italy trip");
            await Assert.That(italy.Trips.Single().StartDate).IsEqualTo(Utc(2025, 5, 1));

            await Assert.That(countries[1].PlaceNames.SequenceEqual(new[] { "Krakow" })).IsTrue();
        }

        [Test]
        public async Task GetVisitedCountriesAsync_WhenCountryIsVisitedByManyTrips_ShouldMergeTripsAndPlaces()
        {
            // Arrange
            var user = await AddUserAsync();
            var warsaw = await AddPlaceAsync("Warsaw", _poland);
            var older = await AddTripAsync(user, Utc(2023, 1, 1), "Older");
            var newer = await AddTripAsync(user, Utc(2025, 1, 1), "Newer");
            await AddSegmentAsync(older, 0, origin: warsaw, destination: _krakow);
            await AddSegmentAsync(newer, 0, origin: _krakow, destination: warsaw);
            await AddSegmentAsync(newer, 1, origin: warsaw, destination: _krakow);
            await AddVisitAsync(older, _poland);
            await AddVisitAsync(newer, _poland);

            // Act
            var result = await _sut.GetVisitedCountriesAsync(user.Id);

            // Assert
            var poland = result.Data!.Single();
            await Assert.That(poland.PlaceNames.SequenceEqual(new[] { "Krakow", "Warsaw" })).IsTrue();
            await Assert.That(poland.Trips.Select(t => t.TripId).SequenceEqual(new[] { newer.Id, older.Id })).IsTrue();
        }

        [Test]
        public async Task GetVisitedCountriesAsync_WhenYearIsSet_ShouldReturnOnlyCountriesAndPlacesOfTripsStartedThatYear()
        {
            // Arrange
            var user = await AddUserAsync();
            var warsaw = await AddPlaceAsync("Warsaw", _poland);
            var trip2024 = await AddTripAsync(user, Utc(2024, 12, 31, 23, 59));
            var trip2025 = await AddTripAsync(user, Utc(2025, 1, 1));
            await AddSegmentAsync(trip2024, 0);
            await AddSegmentAsync(trip2025, 0, origin: warsaw, destination: warsaw);
            await AddVisitAsync(trip2024, _poland);
            await AddVisitAsync(trip2024, _italy);
            await AddVisitAsync(trip2025, _poland);

            // Act
            var result = await _sut.GetVisitedCountriesAsync(user.Id, year: 2025);

            // Assert
            var poland = result.Data!.Single();
            await Assert.That(poland.CountryCode).IsEqualTo("PL");
            await Assert.That(poland.PlaceNames.SequenceEqual(new[] { "Warsaw" })).IsTrue();
            await Assert.That(poland.Trips.Single().TripId).IsEqualTo(trip2025.Id);
        }

        [Test]
        public async Task GetVisitedCountriesAsync_WhenNoTripsInYear_ShouldReturnEmptyList()
        {
            // Arrange
            var user = await AddUserAsync();
            var trip = await AddTripAsync(user, Utc(2025, 1, 1));
            await AddVisitAsync(trip, _poland);

            // Act
            var result = await _sut.GetVisitedCountriesAsync(user.Id, year: 2020);

            // Assert
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(result.Data!).IsEmpty();
        }

        [Test]
        public async Task GetVisitedCountriesAsync_WhenUserHasNoVisits_ShouldReturnEmptyList()
        {
            // Arrange
            var user = await AddUserAsync();

            // Act
            var result = await _sut.GetVisitedCountriesAsync(user.Id);

            // Assert
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(result.Data!).IsEmpty();
        }

        [Test]
        public async Task GetVisitedCountriesAsync_ShouldNotReturnCountriesOfOtherUsers()
        {
            // Arrange
            var user = await AddUserAsync();
            var otherUser = await AddUserAsync();
            var trip = await AddTripAsync(otherUser, Utc(2025, 1, 1));
            await AddVisitAsync(trip, _poland);

            // Act
            var result = await _sut.GetVisitedCountriesAsync(user.Id);

            // Assert
            await Assert.That(result.Data!).IsEmpty();
        }

        [Test]
        public async Task GetVisitedCountriesAsync_ShouldExcludeSoftDeletedTripsAndSegments()
        {
            // Arrange
            var user = await AddUserAsync();
            var warsaw = await AddPlaceAsync("Warsaw", _poland);
            var deletedTrip = await AddTripAsync(user, Utc(2025, 1, 1));
            var activeTrip = await AddTripAsync(user, Utc(2024, 1, 1));
            await AddSegmentAsync(deletedTrip, 0);
            await AddSegmentAsync(activeTrip, 0, origin: _krakow, destination: _krakow);
            var deletedSegment = await AddSegmentAsync(activeTrip, 1, origin: warsaw, destination: warsaw);
            await AddVisitAsync(deletedTrip, _poland);
            await AddVisitAsync(deletedTrip, _italy);
            await AddVisitAsync(activeTrip, _poland);

            _context.Remove(deletedTrip);
            _context.Remove(deletedSegment);
            await _context.SaveChangesAsync();

            // Act
            var result = await _sut.GetVisitedCountriesAsync(user.Id);

            // Assert
            var poland = result.Data!.Single();
            await Assert.That(poland.CountryCode).IsEqualTo("PL");
            await Assert.That(poland.PlaceNames.SequenceEqual(new[] { "Krakow" })).IsTrue();
            await Assert.That(poland.Trips.Single().TripId).IsEqualTo(activeTrip.Id);
        }

        [Test]
        public async Task GetVisitedCountriesAsync_WhenOwnerHasPrivateProfile_ShouldReturnCountries()
        {
            // Arrange
            var owner = await AddUserAsync(isPublic: false);
            var trip = await AddTripAsync(owner, Utc(2025, 1, 1));
            await AddVisitAsync(trip, _poland);

            // Act
            var result = await _sut.GetVisitedCountriesAsync(owner.Id, owner.Id);

            // Assert
            await Assert.That(result.Data!.Single().CountryCode).IsEqualTo("PL");
        }

        [Test]
        public async Task GetVisitedCountriesAsync_WhenTargetProfileIsPublic_ShouldReturnCountries()
        {
            // Arrange
            var requester = await AddUserAsync();
            var target = await AddUserAsync(isPublic: true);
            var trip = await AddTripAsync(target, Utc(2025, 1, 1));
            await AddVisitAsync(trip, _poland);

            // Act
            var result = await _sut.GetVisitedCountriesAsync(requester.Id, target.Id);

            // Assert
            await Assert.That(result.Data!.Single().CountryCode).IsEqualTo("PL");
        }

        [Test]
        public async Task GetVisitedCountriesAsync_WhenTargetProfileIsPrivate_ShouldThrowNotFoundException()
        {
            // Arrange
            var requester = await AddUserAsync();
            var target = await AddUserAsync(isPublic: false);
            var trip = await AddTripAsync(target, Utc(2025, 1, 1));
            await AddVisitAsync(trip, _poland);

            // Act & Assert
            await Assert.That(async () => await _sut.GetVisitedCountriesAsync(requester.Id, target.Id))
                .Throws<NotFoundException>()
                .WithMessage($"User with ID '{target.Id}' was not found.");
        }

        [Test]
        public async Task GetVisitedCountriesAsync_WhenTargetDoesNotExist_ShouldThrowNotFoundException()
        {
            // Arrange
            var requester = await AddUserAsync();
            var targetId = Guid.NewGuid();

            // Act & Assert
            await Assert.That(async () => await _sut.GetVisitedCountriesAsync(requester.Id, targetId))
                .Throws<NotFoundException>()
                .WithMessage($"User with ID '{targetId}' was not found.");
        }

        [Test]
        public async Task GetVisitedCountriesAsync_WhenTargetAccountIsDeleted_ShouldThrowNotFoundException()
        {
            // Arrange
            var requester = await AddUserAsync();
            var target = await AddUserAsync(isPublic: true);
            var trip = await AddTripAsync(target, Utc(2025, 1, 1));
            await AddVisitAsync(trip, _poland);

            _context.Remove(target);
            await _context.SaveChangesAsync();

            // Act & Assert
            await Assert.That(async () => await _sut.GetVisitedCountriesAsync(requester.Id, target.Id))
                .Throws<NotFoundException>();
        }
        #endregion
    }
}
