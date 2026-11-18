using Domain.Common;
using Domain.Const;
using Domain.Exceptions;
using Domain.Models;
using Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;
using NetTopologySuite.Simplify;
using Services.Accessors.Interfaces;
using Services.Command.List;
using Services.Command.Trip;
using Services.Helpers;
using Services.Interfaces;
using Services.QueryExtension;
using Services.Response.Trip;

namespace Services.Services
{
    public class TripServices : ITripServices
    {
        private readonly AppDbContext _context;
        private readonly ILogger<TripServices> _logger;
        private readonly ICancellationTokenAccessor _ctAccessor;

        private CancellationToken _ct => _ctAccessor.Token;
        private CancellationToken CancellationToken => _ct;

        private const double EarthRadiusKm = 6371.0088;


        public TripServices(
            AppDbContext context,
            ILogger<TripServices> logger,
            ICancellationTokenAccessor ctAccessor)
        {
            _context = context;
            _logger = logger;
            _ctAccessor = ctAccessor;
        }

        public async Task<Result<List<TripRouteResponse>>> GetTripRoutesAsync(Guid currentUserId, Guid? targetUserId = null, int? year = null)
        {
            var resolvedUserId = await GetReadableUserIdAsync(currentUserId, targetUserId);

            if (resolvedUserId is null)
            {
                return Result<List<TripRouteResponse>>.Failure(
                    message: "User was not found.",
                    errorCode: ErrorCodes.NotFound,
                    statusCode: StatusCodes.Status404NotFound);
            }

            var readableUserId = resolvedUserId.Value;

            var trips = await _context.Trips
                .AsNoTracking()
                .Where(t => t.UserId == readableUserId && (year == null || t.StartDate.Year == year))
                .OrderByDescending(t => t.StartDate)
                .ThenBy(t => t.Id)
                .Select(t => new TripRouteResponse
                {
                    TripId = t.Id,
                    Title = t.Title,
                    StartDate = t.StartDate,
                    EndDate = t.EndDate,
                    Segments = _context.TripSegments
                        .Where(s => s.TripId == t.Id)
                        .OrderBy(s => s.OrderIndex)
                        .Select(s => new TripRouteSegmentResponse
                        {
                            OrderIndex = s.OrderIndex,
                            TransportType = s.TransportType,
                            DistanceKm = s.DistanceKm,
                            IsFallbackRoute = s.IsFallbackRoute,
                            OriginName = s.OriginPlace.Name,
                            DestinationName = s.DestinationPlace.Name,
                            Coordinates = ToCoordinates(s.Geometry)
                        })
                        .ToList()
                })
                .ToListAsync(_ct);

            _logger.LogInformation("Retrieved {Count} trip routes for user ID: {UserId}, Year: {Year}", trips.Count, readableUserId, year);

            return Result<List<TripRouteResponse>>.Success(
                message: "Trip routes retrieved successfully.",
                data: trips,
                statusCode: StatusCodes.Status200OK);
        }

        public async Task<Result<List<VisitedCountryResponse>>> GetVisitedCountriesAsync(Guid currentUserId, Guid? targetUserId = null, int? year = null)
        {
            var resolvedUserId = await GetReadableUserIdAsync(currentUserId, targetUserId);

            if (resolvedUserId is null)
            {
                return Result<List<VisitedCountryResponse>>.Failure(
                    message: "User was not found.",
                    errorCode: ErrorCodes.NotFound,
                    statusCode: StatusCodes.Status404NotFound);
            }

            var readableUserId = resolvedUserId.Value;

            var visits = await _context.VisitedCountries
                .AsNoTracking()
                .Where(vc => vc.UserId == readableUserId && (year == null || vc.Year == year))
                .Select(vc => new
                {
                    vc.CountryId,
                    vc.Country.Code,
                    vc.Country.Name,
                    Trip = new VisitedCountryTripResponse
                    {
                        TripId = vc.TripId,
                        Title = vc.Trip.Title,
                        StartDate = vc.Trip.StartDate
                    }
                })
                .ToListAsync(_ct);

            var tripIds = visits.Select(v => v.Trip.TripId).Distinct().ToList();
            var segments = _context.TripSegments.Where(s => tripIds.Contains(s.TripId));

            var places = await segments
                .Select(s => new { s.TripId, s.OriginPlace.CountryId, s.OriginPlace.Name })
                .Union(segments.Select(s => new { s.TripId, s.DestinationPlace.CountryId, s.DestinationPlace.Name }))
                .ToListAsync(_ct);

            var placeNamesByVisit = places.ToLookup(p => (p.TripId, p.CountryId), p => p.Name);

            var countries = visits
                .GroupBy(v => new { v.CountryId, v.Code, v.Name })
                .OrderBy(g => g.Key.Name)
                .Select(g => new VisitedCountryResponse
                {
                    CountryCode = g.Key.Code,
                    CountryName = g.Key.Name,
                    PlaceNames = g
                        .SelectMany(v => placeNamesByVisit[(v.Trip.TripId, g.Key.CountryId)])
                        .Distinct()
                        .Order()
                        .ToList(),
                    Trips = g
                        .Select(v => v.Trip)
                        .DistinctBy(t => t.TripId)
                        .OrderByDescending(t => t.StartDate)
                        .ThenBy(t => t.TripId)
                        .ToList()
                })
                .ToList();

            _logger.LogInformation("Retrieved {Count} visited countries for user ID: {UserId}, Year: {Year}", countries.Count, readableUserId, year);

            return Result<List<VisitedCountryResponse>>.Success(
                message: "Visited countries retrieved successfully.",
                data: countries,
                statusCode: StatusCodes.Status200OK);
        }

        public async Task<Result<PagedResult<TripSummaryResponse>>> GetUserTripsAsync(
          BasicListCommand command,
          Guid userId,
          CancellationToken ct = default)
        {
            var token = _ctAccessor.ResolveCancellationToken(ct);
            token.ThrowIfCancellationRequested();

            return await _context.Trips
                .AsNoTracking()
                .Where(t => t.UserId == userId)
                .ApplySearch(command.SearchTerm)
                .ApplySorting(command.SortBy, command.SortDescending)
                .Select(t => new TripSummaryResponse
                {
                    Id = t.Id,
                    Title = t.Title,
                    Description = t.Description == null
                        ? null
                        : t.Description.Length > 100
                            ? t.Description.Substring(0, 100) + "..."
                            : t.Description,
                    StartDate = t.StartDate,
                    EndDate = t.EndDate,
                    TotalDistance = _context.TripSegments
                        .Where(s => s.TripId == t.Id)
                        .Sum(s => (decimal?)s.DistanceKm) ?? 0m,
                    TransportTypes = _context.TripSegments
                        .Where(s => s.TripId == t.Id)
                        .Select(s => s.TransportType)
                        .Distinct()
                        .ToList()
                })
                .ToPagedResultAsync(command.PageNumber, command.PageSize, _logger, "Trips", token);
        }

        private async Task<Guid?> GetReadableUserIdAsync(
            Guid currentUserId,
            Guid? targetUserId)
        {
            var resolvedUserId = targetUserId ?? currentUserId;
            var isOwner = resolvedUserId == currentUserId;

            var canRead = await _context.Users
                .AnyAsync(
                    user => user.Id == resolvedUserId && (isOwner || user.IsPublic),
                    _ct);

            return canRead ? resolvedUserId : null;
        }

        public async Task<Result<TripDetailsResponse>> CreateTripAsync(
            Guid userId,
            UpsertTripCommand command)
        {
            var (startDate, endDate) = NormalizeDates(command);

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

            var segmentsResult = await CreateSegmentsAsync(trip, command.Segments);

            if (!segmentsResult.IsSuccess)
            {
                await transaction.RollbackAsync(CancellationToken);

                return Result<TripDetailsResponse>.Failure(
                    message: segmentsResult.Message ?? "A trip point is invalid.",
                    errorCode: segmentsResult.ErrorCode ?? ErrorCodes.TripPointInvalid,
                    statusCode: segmentsResult.StatusCode);
            }

            var segments = segmentsResult.Data!;

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
            var (startDate, endDate) = NormalizeDates(command);

            await using var transaction =
                await _context.Database.BeginTransactionAsync(CancellationToken);

            var trip = await _context.Trips
                .FirstOrDefaultAsync(
                    item => item.Id == tripId && item.UserId == userId,
                    CancellationToken);

            if (trip is null)
            {
                await transaction.RollbackAsync(CancellationToken);

                return Result<TripDetailsResponse>.Failure(
                    message: "Trip was not found.",
                    errorCode: ErrorCodes.NotFound,
                    statusCode: StatusCodes.Status404NotFound);
            }

            var existingSegments = await _context.TripSegments
                .Where(segment => segment.TripId == tripId)
                .ToListAsync(CancellationToken);

            _context.TripSegments.RemoveRange(existingSegments);

            var existingVisits = await _context.VisitedCountries
                .Where(visit => visit.TripId == tripId)
                .ToListAsync(CancellationToken);

            _context.VisitedCountries.RemoveRange(existingVisits);

            trip.Title = command.Title.Trim();
            trip.Description = command.Description;
            trip.StartDate = startDate;
            trip.EndDate = endDate;

            await _context.SaveChangesAsync(CancellationToken);

            var segmentsResult = await CreateSegmentsAsync(trip, command.Segments);

            if (!segmentsResult.IsSuccess)
            {
                await transaction.RollbackAsync(CancellationToken);

                return Result<TripDetailsResponse>.Failure(
                    message: segmentsResult.Message ?? "A trip point is invalid.",
                    errorCode: segmentsResult.ErrorCode ?? ErrorCodes.TripPointInvalid,
                    statusCode: segmentsResult.StatusCode);
            }

            var newSegments = segmentsResult.Data!;

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
                return Result<TripDetailsResponse>.Failure(
                    message: "Trip was not found.",
                    errorCode: ErrorCodes.NotFound,
                    statusCode: StatusCodes.Status404NotFound);
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
                await transaction.RollbackAsync(CancellationToken);

                return Result.Failure(
                    message: "Trip was not found.",
                    errorCode: ErrorCodes.NotFound,
                    statusCode: StatusCodes.Status404NotFound);
            }

            var segments = await _context.TripSegments
                .Where(segment => segment.TripId == tripId)
                .ToListAsync(CancellationToken);

            var visits = await _context.VisitedCountries
                .Where(visit => visit.TripId == tripId)
                .ToListAsync(CancellationToken);

            _context.Trips.Remove(trip);
            _context.TripSegments.RemoveRange(segments);
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

        private async Task<Result<List<TripSegment>>> CreateSegmentsAsync(
            Trip trip,
            IReadOnlyList<TripSegmentCommand> commands)
        {
            var countries = await _context.Countries
                .AsNoTracking()
                .OrderBy(country => country.Code)
                .ToListAsync(CancellationToken);

            if (countries.Count == 0)
            {
                throw new InvalidOperationException("No countries are configured.");
            }

            var segments = new List<TripSegment>(commands.Count);
            var places = new List<Place>(commands.Count * 2);

            for (var index = 0; index < commands.Count; index++)
            {
                var command = commands[index];

                var origin = CreatePlace(command.Origin, countries);
                var destination = CreatePlace(command.Destination, countries);

                if (origin is null || destination is null)
                {
                    return Result<List<TripSegment>>.Failure(
                        message: "A trip point must be inside a configured country.",
                        errorCode: ErrorCodes.TripPointInvalid,
                        statusCode: StatusCodes.Status400BadRequest);
                }

                places.Add(origin);
                places.Add(destination);

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

            _context.Places.AddRange(places);

            return Result<List<TripSegment>>.Success(
                message: "Trip segments created successfully.",
                statusCode: StatusCodes.Status200OK,
                data: segments);
        }

        private static Place? CreatePlace(
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
                return null;
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

        private static (DateTime StartDate, DateTime? EndDate) NormalizeDates(
            UpsertTripCommand command)
        {
            var startDate = NormalizeUtc(command.StartDate);
            DateTime? endDate = command.EndDate.HasValue
                ? NormalizeUtc(command.EndDate.Value)
                : null;

            return (startDate, endDate);
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

        private static List<GeoCoordinate> ToCoordinates(Geometry geometry)
            => DouglasPeuckerSimplifier.Simplify(geometry, GeoConst.RouteSimplificationToleranceDegrees)
                .Coordinates
                .Select(c => new GeoCoordinate { Latitude = c.Y, Longitude = c.X })
                .ToList();
    }
}
