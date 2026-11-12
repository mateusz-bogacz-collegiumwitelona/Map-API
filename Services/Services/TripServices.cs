using Domain.Common;
using Domain.Const;
using Domain.Exceptions;
using Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;
using NetTopologySuite.Simplify;
using Services.Accessors.Interfaces;
using Services.Command.List;
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
            var resolvedUserId = await EnsureCanReadAsync(currentUserId, targetUserId);

            var trips = await _context.Trips
                .AsNoTracking()
                .Where(t => t.UserId == resolvedUserId && (year == null || t.StartDate.Year == year))
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

            _logger.LogInformation("Retrieved {Count} trip routes for user ID: {UserId}, Year: {Year}", trips.Count, resolvedUserId, year);

            return Result<List<TripRouteResponse>>.Success(
                message: "Trip routes retrieved successfully.",
                data: trips,
                statusCode: StatusCodes.Status200OK);
        }

        public async Task<Result<List<VisitedCountryResponse>>> GetVisitedCountriesAsync(Guid currentUserId, Guid? targetUserId = null, int? year = null)
        {
            var resolvedUserId = await EnsureCanReadAsync(currentUserId, targetUserId);

            var visits = await _context.VisitedCountries
                .AsNoTracking()
                .Where(vc => vc.UserId == resolvedUserId && (year == null || vc.Year == year))
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

            _logger.LogInformation("Retrieved {Count} visited countries for user ID: {UserId}, Year: {Year}", countries.Count, resolvedUserId, year);

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

        private async Task<Guid> EnsureCanReadAsync(Guid currentUserId, Guid? targetUserId)
        {
            var resolvedUserId = targetUserId ?? currentUserId;
            var isOwner = resolvedUserId == currentUserId;

            var canRead = await _context.Users
                .AnyAsync(u => u.Id == resolvedUserId && (isOwner || u.IsPublic), _ct);

            if (!canRead)
            {
                throw new NotFoundException($"User with ID '{resolvedUserId}' was not found.");
            }

            return resolvedUserId;
        }

        private static List<GeoCoordinate> ToCoordinates(Geometry geometry)
            => DouglasPeuckerSimplifier.Simplify(geometry, GeoConst.RouteSimplificationToleranceDegrees)
                .Coordinates
                .Select(c => new GeoCoordinate { Latitude = c.Y, Longitude = c.X })
                .ToList();
    }
}
