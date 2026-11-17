using Domain.Common;
using Domain.Const;
using Domain.Exceptions;
using Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;
using Services.Accessors.Interfaces;
using Services.Command.Trip;
using Services.Helpers;
using Services.Interfaces;

namespace Services.Services
{
    public class TripSegmentServices : ITripSegmentServices
    {
        private readonly AppDbContext _context;
        private readonly ILogger<TripSegmentServices> _logger;
        private readonly ICancellationTokenAccessor _ctAccessor;

        private CancellationToken _ct => _ctAccessor.Token;

        public TripSegmentServices(
            AppDbContext context,
            ILogger<TripSegmentServices> logger,
            ICancellationTokenAccessor ctAccessor)
        {
            _context = context;
            _logger = logger;
            _ctAccessor = ctAccessor;
        }

        public async Task<Result> UpdateRouteAsync(Guid userId, Guid segmentId, UpdateSegmentRouteCommand command)
        {
            var segment = await _context.TripSegments
                .Include(s => s.OriginPlace)
                .Include(s => s.DestinationPlace)
                .FirstOrDefaultAsync(s => s.Id == segmentId && s.Trip.UserId == userId, _ct)
                ?? throw new NotFoundException($"Trip segment with ID '{segmentId}' was not found.");

            var start = command.Coordinates[0];
            var end = command.Coordinates[^1];

            if (!IsNear(start, segment.OriginPlace.Location) || !IsNear(end, segment.DestinationPlace.Location))
            {
                _logger.LogWarning("Route for segment ID: {SegmentId} does not connect its origin and destination", segmentId);

                return Result.Failure(
                    message: FormattableString.Invariant($"Route must start within {TripConst.RouteEndpointToleranceKm} km of the segment origin and end within {TripConst.RouteEndpointToleranceKm} km of its destination."),
                    errorCode: ErrorCodes.RouteEndpointsMismatch,
                    statusCode: StatusCodes.Status400BadRequest);
            }

            var routeKm = RouteLengthKm(command.Coordinates);

            if ((double)command.DistanceKm + TripConst.RouteDistanceRoundingKm < routeKm * (1 - TripConst.RouteDistanceTolerance))
            {
                _logger.LogWarning("Distance {DistanceKm} km for segment ID: {SegmentId} is shorter than the route length {RouteKm} km", command.DistanceKm, segmentId, routeKm);

                return Result.Failure(
                    message: "Distance cannot be shorter than the length of the route line.",
                    errorCode: ErrorCodes.DistanceInvalid,
                    statusCode: StatusCodes.Status400BadRequest);
            }

            segment.Geometry = new LineString(command.Coordinates
                .Select(p => new Coordinate(p.Longitude, p.Latitude))
                .ToArray())
            { SRID = GeoConst.SRID };
            segment.DistanceKm = command.DistanceKm;
            segment.IsFallbackRoute = command.IsFallbackRoute;

            await _context.SaveChangesAsync(_ct);

            _logger.LogInformation("Updated route of segment ID: {SegmentId} with {Count} points and {DistanceKm} km", segmentId, command.Coordinates.Count, command.DistanceKm);

            return Result.Success(
                message: "Segment route updated successfully.",
                statusCode: StatusCodes.Status200OK);
        }

        private static double RouteLengthKm(IReadOnlyList<RoutePointCommand> points)
            => points
                .Zip(points.Skip(1), (from, to) => GeoDistanceHelper.HaversineKm(from.Latitude, from.Longitude, to.Latitude, to.Longitude))
                .Sum();

        private static bool IsNear(RoutePointCommand point, Point place)
            => GeoDistanceHelper.HaversineKm(point.Latitude, point.Longitude, place.Y, place.X) <= TripConst.RouteEndpointToleranceKm;
    }
}
