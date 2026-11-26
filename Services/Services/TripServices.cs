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
                            SegmentId = s.Id,
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
        public async Task<Result> UpdateSegmentRouteAsync(Guid userId, Guid segmentId, UpdateSegmentRouteCommand command)
        {
            var points = RemoveConsecutiveDuplicates(command.Coordinates);

            if (points.Count < TripConst.MinRoutePoints)
            {
                _logger.LogWarning("Route for segment ID: {SegmentId} has fewer than {MinRoutePoints} distinct points", segmentId, TripConst.MinRoutePoints);

                return Result.Failure(
                    message: $"Route must have at least {TripConst.MinRoutePoints} distinct points.",
                    errorCode: ErrorCodes.RouteInvalid,
                    statusCode: StatusCodes.Status400BadRequest);
            }

            var found = await _context.TripSegments
                .Where(s => s.Id == segmentId && s.Trip.UserId == userId)
                .Select(s => new
                {
                    Segment = s,
                    Origin = s.OriginPlace.Location,
                    Destination = s.DestinationPlace.Location
                })
                .FirstOrDefaultAsync(_ct);

            if (found == null)
            {
                _logger.LogWarning("Trip segment ID: {SegmentId} was not found for user ID: {UserId}", segmentId, userId);

                return Result.Failure(
                    message: $"Trip segment with ID '{segmentId}' was not found.",
                    errorCode: ErrorCodes.NotFound,
                    statusCode: StatusCodes.Status404NotFound);
            }

            if (!IsNear(points[0], found.Origin) || !IsNear(points[^1], found.Destination))
            {
                _logger.LogWarning("Route for segment ID: {SegmentId} does not connect its origin and destination", segmentId);

                return Result.Failure(
                    message: FormattableString.Invariant($"Route must start within {TripConst.RouteEndpointToleranceKm} km of the segment origin and end within {TripConst.RouteEndpointToleranceKm} km of its destination."),
                    errorCode: ErrorCodes.RouteEndpointsMismatch,
                    statusCode: StatusCodes.Status400BadRequest);
            }

            var routeKm = RouteLengthKm(points);
            var distanceKm = (double)command.DistanceKm;

            if (distanceKm + TripConst.RouteDistanceRoundingKm < routeKm * (1 - TripConst.RouteDistanceTolerance))
            {
                _logger.LogWarning("Distance {DistanceKm} km for segment ID: {SegmentId} is shorter than the route length {RouteKm} km", command.DistanceKm, segmentId, routeKm);

                return Result.Failure(
                    message: "Distance cannot be shorter than the length of the route line.",
                    errorCode: ErrorCodes.DistanceInvalid,
                    statusCode: StatusCodes.Status400BadRequest);
            }

            var maxRatio = command.IsFallbackRoute ? 1 + TripConst.RouteDistanceTolerance : TripConst.MaxRouteDistanceRatio;

            if (distanceKm - TripConst.RouteDistanceRoundingKm > routeKm * maxRatio)
            {
                _logger.LogWarning("Distance {DistanceKm} km for segment ID: {SegmentId} is more than {MaxRatio} times the route length {RouteKm} km", command.DistanceKm, segmentId, maxRatio, routeKm);

                return Result.Failure(
                    message: FormattableString.Invariant($"Distance cannot be more than {maxRatio} times the length of the route line."),
                    errorCode: ErrorCodes.DistanceInvalid,
                    statusCode: StatusCodes.Status400BadRequest);
            }

            var geometry = new LineString(points
                .Select(p => new Coordinate(p.Longitude, p.Latitude))
                .ToArray())
            { SRID = GeoConst.SRID };

            found.Segment.UpdateRoute(geometry, command.DistanceKm, command.IsFallbackRoute);

            await _context.SaveChangesAsync(_ct);

            _logger.LogInformation("Updated route of segment ID: {SegmentId} with {Count} points and {DistanceKm} km", segmentId, points.Count, command.DistanceKm);

            return Result.Success(
                message: "Segment route updated successfully.",
                statusCode: StatusCodes.Status200OK);
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

            var existingVisits = await _context.VisitedCountries
                .Where(visit => visit.TripId == tripId)
                .ToListAsync(CancellationToken);

            _context.VisitedCountries.RemoveRange(existingVisits);

            trip.Title = command.Title.Trim();
            trip.Description = command.Description;
            trip.StartDate = startDate;
            trip.EndDate = endDate;

            await _context.SaveChangesAsync(CancellationToken);

            var segmentsResult = await CreateSegmentsAsync(
                trip,
                command.Segments,
                existingSegments);

            if (!segmentsResult.IsSuccess)
            {
                await transaction.RollbackAsync(CancellationToken);

                return Result<TripDetailsResponse>.Failure(
                    message: segmentsResult.Message ?? "A trip point is invalid.",
                    errorCode: segmentsResult.ErrorCode ?? ErrorCodes.TripPointInvalid,
                    statusCode: segmentsResult.StatusCode);
            }

            var updatedSegments = segmentsResult.Data!;
            var existingSegmentIds = existingSegments
                .Select(segment => segment.Id)
                .ToHashSet();

            var retainedSegmentIds = updatedSegments
                .Where(segment => existingSegmentIds.Contains(segment.Id))
                .Select(segment => segment.Id)
                .ToHashSet();

            _context.TripSegments.RemoveRange(
                existingSegments.Where(segment =>
                    !retainedSegmentIds.Contains(segment.Id)));

            _context.TripSegments.AddRange(
                updatedSegments.Where(segment =>
                    !existingSegmentIds.Contains(segment.Id)));

            AddVisitedCountries(trip, updatedSegments);

            await _context.SaveChangesAsync(CancellationToken);
            await transaction.CommitAsync(CancellationToken);

            _logger.LogInformation(
                "Updated trip {TripId} for user {UserId}",
                tripId,
                userId);

            return Result<TripDetailsResponse>.Success(
                message: "Trip updated successfully.",
                statusCode: StatusCodes.Status200OK,
                data: ToResponse(trip, updatedSegments));
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
    IReadOnlyList<TripSegmentCommand> commands,
    IReadOnlyList<TripSegment>? existingSegments = null)
        {
            var placeIds = commands
                .SelectMany(command => new[]
                {
            command.Origin.PlaceId,
            command.Destination.PlaceId
                })
                .Distinct()
                .ToList();

            var placesById = await _context.Places
                .Where(place =>
                    place.IsCatalogPlace &&
                    placeIds.Contains(place.Id))
                .ToDictionaryAsync(
                    place => place.Id,
                    CancellationToken);

            if (placesById.Count != placeIds.Count)
            {
                return Result<List<TripSegment>>.Failure(
                    message: "One or more trip points were not found.",
                    errorCode: ErrorCodes.TripPointInvalid,
                    statusCode: StatusCodes.Status400BadRequest);
            }

            var segments = new List<TripSegment>(commands.Count);
            var matchedSegmentIds = new HashSet<Guid>();

            for (var index = 0; index < commands.Count; index++)
            {
                var command = commands[index];
                var origin = placesById[command.Origin.PlaceId];
                var destination = placesById[command.Destination.PlaceId];

                var existingSegment = existingSegments?.FirstOrDefault(segment =>
                    !matchedSegmentIds.Contains(segment.Id) &&
                    segment.OriginPlaceId == origin.Id &&
                    segment.DestinationPlaceId == destination.Id &&
                    segment.TransportType == command.TransportType);

                if (existingSegment is not null)
                {
                    matchedSegmentIds.Add(existingSegment.Id);
                    existingSegment.OrderIndex = index;
                    existingSegment.OriginPlace = origin;
                    existingSegment.DestinationPlace = destination;
                    segments.Add(existingSegment);

                    continue;
                }

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
                    OriginPlaceId = origin.Id,
                    OriginPlace = origin,
                    DestinationPlaceId = destination.Id,
                    DestinationPlace = destination,
                    DistanceKm = CalculateDistanceKm(
                        origin.Location,
                        destination.Location),
                    Geometry = geometry,
                    IsFallbackRoute = true
                });
            }

            return Result<List<TripSegment>>.Success(
                message: "Trip segments created successfully.",
                statusCode: StatusCodes.Status200OK,
                data: segments);
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
            var distance = GeoDistanceHelper.HaversineKm(
                origin.Y,
                origin.X,
                destination.Y,
                destination.X);

            return decimal.Round(
                (decimal)distance,
                2,
                MidpointRounding.AwayFromZero);
        }

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
                        SegmentId = segment.Id,
                        OrderIndex = segment.OrderIndex,
                        TransportType = segment.TransportType,
                        DistanceKm = segment.DistanceKm,
                        IsFallbackRoute = segment.IsFallbackRoute,
                        Origin = new TripPointResponse
                        {
                            PlaceId = segment.OriginPlaceId,
                            Name = segment.OriginPlace.Name,
                            Latitude = segment.OriginPlace.Location.Y,
                            Longitude = segment.OriginPlace.Location.X
                        },
                        Destination = new TripPointResponse
                        {
                            PlaceId = segment.DestinationPlaceId,
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

        private static List<RoutePointCommand> RemoveConsecutiveDuplicates(IReadOnlyList<RoutePointCommand>? points)
        {
            var result = new List<RoutePointCommand>(points?.Count ?? 0);

            if (points == null)
            {
                return result;
            }

            foreach (var point in points)
            {
                if (point != null && (result.Count == 0 || result[^1] != point))
                {
                    result.Add(point);
                }
            }

            return result;
        }

        private static double RouteLengthKm(List<RoutePointCommand> points)
        {
            var length = 0.0;

            for (var i = 1; i < points.Count; i++)
            {
                length += GeoDistanceHelper.HaversineKm(points[i - 1].Latitude, points[i - 1].Longitude, points[i].Latitude, points[i].Longitude);
            }

            return length;
        }

        private static bool IsNear(RoutePointCommand point, Point place)
            => GeoDistanceHelper.HaversineKm(point.Latitude, point.Longitude, place.Y, place.X) <= TripConst.RouteEndpointToleranceKm;
    }
}