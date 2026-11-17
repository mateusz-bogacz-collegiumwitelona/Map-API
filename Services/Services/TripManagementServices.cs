using Domain.Common;
using Domain.Const;
using Domain.Exceptions;
using Domain.Models;
using Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;
using Services.Accessors.Interfaces;
using Services.Command.Trip;
using Services.Interfaces;
using Services.Response.Trip;

namespace Services.Services
{
    public sealed class TripManagementServices : ITripManagementServices
    {
        private const double EarthRadiusKm = 6371.0088;

        private readonly AppDbContext _context;
        private readonly ILogger<TripManagementServices> _logger;
        private readonly ICancellationTokenAccessor _ctAccessor;

        private CancellationToken CancellationToken => _ctAccessor.Token;

        public TripManagementServices(
            AppDbContext context,
            ILogger<TripManagementServices> logger,
            ICancellationTokenAccessor ctAccessor)
        {
            _context = context;
            _logger = logger;
            _ctAccessor = ctAccessor;
        }

        public async Task<Result<TripDetailsResponse>> CreateTripAsync(
            Guid userId,
            UpsertTripCommand command)
        {
            var (startDate, endDate) = ValidateAndNormalize(command);

            await using var transaction =
                await _context.Database.BeginTransactionAsync(CancellationToken);

            var trip = new Trip
            {
                UserId = userId,
                Title = command.Title.Trim(),
                Description = command.Description,
                StartDate = startDate,
                EndDate = endDate
            };

            _context.Trips.Add(trip);

            await _context.SaveChangesAsync(CancellationToken);

            var segments = await CreateSegmentsAsync(trip, command.Segments);

            _context.TripSegments.AddRange(segments);
            AddVisitedCountries(trip, segments);

            await _context.SaveChangesAsync(CancellationToken);
            await transaction.CommitAsync(CancellationToken);

            _logger.LogInformation(
                "Created trip {TripId} for user {UserId}",
                trip.Id,
                userId);

            return Result<TripDetailsResponse>.Success(
                message: "Trip created successfully.",
                statusCode: StatusCodes.Status201Created,
                data: ToResponse(trip, segments));
        }

        public async Task<Result<TripDetailsResponse>> UpdateTripAsync(
            Guid userId,
            Guid tripId,
            UpsertTripCommand command)
        {
            var (startDate, endDate) = ValidateAndNormalize(command);

            await using var transaction =
                await _context.Database.BeginTransactionAsync(CancellationToken);

            var trip = await _context.Trips
                .FirstOrDefaultAsync(
                    item => item.Id == tripId && item.UserId == userId,
                    CancellationToken);

            if (trip is null)
            {
                throw new NotFoundException("Trip was not found.");
            }

            var now = DateTime.UtcNow;

            var existingSegments = await _context.TripSegments
                .Where(segment => segment.TripId == tripId)
                .ToListAsync(CancellationToken);

            foreach (var segment in existingSegments)
            {
                segment.IsDeleted = true;
                segment.DeletedAt = now;
            }

            var existingVisits = await _context.VisitedCountries
                .Where(visit => visit.TripId == tripId)
                .ToListAsync(CancellationToken);

            _context.VisitedCountries.RemoveRange(existingVisits);

            trip.Title = command.Title.Trim();
            trip.Description = command.Description;
            trip.StartDate = startDate;
            trip.EndDate = endDate;

            await _context.SaveChangesAsync(CancellationToken);

            var newSegments = await CreateSegmentsAsync(trip, command.Segments);

            _context.TripSegments.AddRange(newSegments);
            AddVisitedCountries(trip, newSegments);

            await _context.SaveChangesAsync(CancellationToken);
            await transaction.CommitAsync(CancellationToken);

            _logger.LogInformation(
                "Updated trip {TripId} for user {UserId}",
                tripId,
                userId);

            return Result<TripDetailsResponse>.Success(
                message: "Trip updated successfully.",
                statusCode: StatusCodes.Status200OK,
                data: ToResponse(trip, newSegments));
        }

        public async Task<Result<TripDetailsResponse>> GetTripDetailsAsync(
            Guid userId,
            Guid tripId)
        {
            var trip = await _context.Trips
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    item => item.Id == tripId && item.UserId == userId,
                    CancellationToken);

            if (trip is null)
            {
                throw new NotFoundException("Trip was not found.");
            }

            var segments = await _context.TripSegments
                .AsNoTracking()
                .Where(segment => segment.TripId == tripId)
                .Include(segment => segment.OriginPlace)
                .Include(segment => segment.DestinationPlace)
                .OrderBy(segment => segment.OrderIndex)
                .ToListAsync(CancellationToken);

            return Result<TripDetailsResponse>.Success(
                message: "Trip details retrieved successfully.",
                statusCode: StatusCodes.Status200OK,
                data: ToResponse(trip, segments));
        }

        public async Task<Result> DeleteTripAsync(Guid userId, Guid tripId)
        {
            await using var transaction =
                await _context.Database.BeginTransactionAsync(CancellationToken);

            var trip = await _context.Trips
                .FirstOrDefaultAsync(
                    item => item.Id == tripId && item.UserId == userId,
                    CancellationToken);

            if (trip is null)
            {
                throw new NotFoundException("Trip was not found.");
            }

            var now = DateTime.UtcNow;

            trip.IsDeleted = true;
            trip.DeletedAt = now;

            var segments = await _context.TripSegments
                .Where(segment => segment.TripId == tripId)
                .ToListAsync(CancellationToken);

            foreach (var segment in segments)
            {
                segment.IsDeleted = true;
                segment.DeletedAt = now;
            }

            var visits = await _context.VisitedCountries
                .Where(visit => visit.TripId == tripId)
                .ToListAsync(CancellationToken);

            _context.VisitedCountries.RemoveRange(visits);

            await _context.SaveChangesAsync(CancellationToken);
            await transaction.CommitAsync(CancellationToken);

            _logger.LogInformation(
                "Soft-deleted trip {TripId} for user {UserId}",
                tripId,
                userId);

            return Result.Success(
                message: "Trip deleted successfully.",
                statusCode: StatusCodes.Status200OK);
        }

        private async Task<List<TripSegment>> CreateSegmentsAsync(
            Trip trip,
            IReadOnlyList<TripSegmentCommand> commands)
        {
            var countries = await _context.Countries
                .AsNoTracking()
                .OrderBy(country => country.Code)
                .ToListAsync(CancellationToken);

            if (countries.Count == 0)
            {
                throw new ArgumentException("No countries are configured.");
            }

            var segments = new List<TripSegment>(commands.Count);

            for (var index = 0; index < commands.Count; index++)
            {
                var command = commands[index];

                if (!Enum.IsDefined(command.TransportType))
                {
                    throw new ArgumentException("Transport type is invalid.");
                }

                var origin = CreatePlace(command.Origin, countries);
                var destination = CreatePlace(command.Destination, countries);

                _context.Places.AddRange(origin, destination);

                var geometry = new LineString(
                    new[]
                    {
                        origin.Location.Coordinate,
                        destination.Location.Coordinate
                    })
                {
                    SRID = GeoConst.SRID
                };

                segments.Add(new TripSegment
                {
                    Trip = trip,
                    OrderIndex = index,
                    TransportType = command.TransportType,
                    OriginPlace = origin,
                    DestinationPlace = destination,
                    DistanceKm = CalculateDistanceKm(
                        origin.Location,
                        destination.Location),
                    Geometry = geometry,
                    IsFallbackRoute = true
                });
            }

            return segments;
        }

        private static Place CreatePlace(
            TripPointCommand point,
            IReadOnlyList<Country> countries)
        {
            var location = new Point(point.Longitude, point.Latitude)
            {
                SRID = GeoConst.SRID
            };

            var country = countries.FirstOrDefault(
                candidate => candidate.Boundary.Covers(location));

            if (country is null)
            {
                throw new ArgumentException(
                    $"The point '{point.Name}' is not inside a known country.");
            }

            return new Place
            {
                Name = point.Name.Trim(),
                CountryId = country.Id,
                Location = location
            };
        }

        private void AddVisitedCountries(
            Trip trip,
            IEnumerable<TripSegment> segments)
        {
            var countryIds = segments
                .SelectMany(segment => new[]
                {
                    segment.OriginPlace.CountryId,
                    segment.DestinationPlace.CountryId
                })
                .Distinct();

            foreach (var countryId in countryIds)
            {
                _context.VisitedCountries.Add(new VisitedCountry
                {
                    UserId = trip.UserId,
                    TripId = trip.Id,
                    CountryId = countryId,
                    Year = trip.StartDate.Year
                });
            }
        }

        private static decimal CalculateDistanceKm(Point origin, Point destination)
        {
            var latitude1 = DegreesToRadians(origin.Y);
            var latitude2 = DegreesToRadians(destination.Y);
            var latitudeDelta = DegreesToRadians(destination.Y - origin.Y);
            var longitudeDelta = DegreesToRadians(destination.X - origin.X);

            var a =
                Math.Pow(Math.Sin(latitudeDelta / 2), 2) +
                Math.Cos(latitude1) *
                Math.Cos(latitude2) *
                Math.Pow(Math.Sin(longitudeDelta / 2), 2);

            a = Math.Clamp(a, 0d, 1d);

            var distance = EarthRadiusKm *
                2d *
                Math.Atan2(Math.Sqrt(a), Math.Sqrt(1d - a));

            return decimal.Round(
                (decimal)distance,
                2,
                MidpointRounding.AwayFromZero);
        }

        private static double DegreesToRadians(double degrees)
            => degrees * Math.PI / 180d;

        private static (DateTime StartDate, DateTime? EndDate) ValidateAndNormalize(
            UpsertTripCommand command)
        {
            if (string.IsNullOrWhiteSpace(command.Title))
            {
                throw new ArgumentException("Trip title is required.");
            }

            if (command.Title.Length > 200)
            {
                throw new ArgumentException("Trip title cannot exceed 200 characters.");
            }

            if (command.Description?.Length > 2000)
            {
                throw new ArgumentException(
                    "Trip description cannot exceed 2000 characters.");
            }

            if (command.Segments is null || command.Segments.Count == 0)
            {
                throw new ArgumentException(
                    "A trip must contain at least one segment.");
            }

            foreach (var segment in command.Segments)
            {
                ValidatePoint(segment.Origin);
                ValidatePoint(segment.Destination);
            }

            if (!HaveContinuousSegments(command.Segments))
            {
                throw new ArgumentException(
                    "Each segment must start where the previous segment ends.");
            }

            var startDate = NormalizeUtc(command.StartDate);
            DateTime? endDate = command.EndDate.HasValue
                ? NormalizeUtc(command.EndDate.Value)
                : null;

            if (endDate.HasValue && endDate.Value < startDate)
            {
                throw new ArgumentException(
                    "End date cannot be earlier than start date.");
            }

            return (startDate, endDate);
        }

        private static void ValidatePoint(TripPointCommand point)
        {
            if (string.IsNullOrWhiteSpace(point.Name) || point.Name.Length > 255)
            {
                throw new ArgumentException(
                    "Point name must contain between 1 and 255 characters.");
            }

            if (!double.IsFinite(point.Latitude) ||
                point.Latitude < -90d ||
                point.Latitude > 90d)
            {
                throw new ArgumentException("Latitude is invalid.");
            }

            if (!double.IsFinite(point.Longitude) ||
                point.Longitude < -180d ||
                point.Longitude > 180d)
            {
                throw new ArgumentException("Longitude is invalid.");
            }
        }

        private static bool HaveContinuousSegments(
        IReadOnlyList<TripSegmentCommand> segments)
        {
            const double coordinateTolerance = 0.000001d;

            for (var index = 1; index < segments.Count; index++)
            {
                var previousDestination = segments[index - 1].Destination;
                var currentOrigin = segments[index].Origin;

                if (Math.Abs(
                        previousDestination.Latitude - currentOrigin.Latitude) >
                        coordinateTolerance ||
                    Math.Abs(
                        previousDestination.Longitude - currentOrigin.Longitude) >
                        coordinateTolerance)
                {
                    return false;
                }
            }

            return true;
        }

        private static DateTime NormalizeUtc(DateTime value)
        {
            return value.Kind switch
            {
                DateTimeKind.Utc => value,
                DateTimeKind.Local => value.ToUniversalTime(),
                _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
            };
        }

        private static TripDetailsResponse ToResponse(
            Trip trip,
            IEnumerable<TripSegment> segments)
        {
            return new TripDetailsResponse
            {
                Id = trip.Id,
                Title = trip.Title,
                Description = trip.Description,
                StartDate = trip.StartDate,
                EndDate = trip.EndDate,
                Segments = segments
                    .OrderBy(segment => segment.OrderIndex)
                    .Select(segment => new TripDetailsSegmentResponse
                    {
                        OrderIndex = segment.OrderIndex,
                        TransportType = segment.TransportType,
                        DistanceKm = segment.DistanceKm,
                        IsFallbackRoute = segment.IsFallbackRoute,
                        Origin = new TripPointResponse
                        {
                            Name = segment.OriginPlace.Name,
                            Latitude = segment.OriginPlace.Location.Y,
                            Longitude = segment.OriginPlace.Location.X
                        },
                        Destination = new TripPointResponse
                        {
                            Name = segment.DestinationPlace.Name,
                            Latitude = segment.DestinationPlace.Location.Y,
                            Longitude = segment.DestinationPlace.Location.X
                        }
                    })
                    .ToList()
            };
        }
    }
}