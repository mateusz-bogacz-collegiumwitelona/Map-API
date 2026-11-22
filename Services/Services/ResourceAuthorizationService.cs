using Domain.Exceptions;
using Infrastructure;
using Microsoft.EntityFrameworkCore;
using Services.Interfaces;

namespace Services.Services
{
    public class ResourceAuthorizationService : IResourceAuthorizationService
    {
        private readonly AppDbContext _context;

        public ResourceAuthorizationService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<Guid> EnsureCanReadProfileAsync(
            Guid currentUserId,
            Guid? targetUserId = null,
            CancellationToken ct = default)
        {
            var resolvedUserId = targetUserId ?? currentUserId;
            var isOwner = resolvedUserId == currentUserId;

            var canRead = await _context.Users
                .AsNoTracking()
                .AnyAsync(
                    user => user.Id == resolvedUserId && (isOwner || user.IsPublic),
                    ct);

            if (!canRead)
            {
                // Private profiles are reported as missing so their existence is not disclosed.
                throw new NotFoundException($"User with ID '{resolvedUserId}' was not found.");
            }

            return resolvedUserId;
        }

        public async Task EnsureCanModifyTripAsync(
            Guid currentUserId,
            Guid tripId,
            CancellationToken ct = default)
        {
            var ownerId = await _context.Trips
                .AsNoTracking()
                .Where(trip => trip.Id == tripId)
                .Select(trip => (Guid?)trip.UserId)
                .FirstOrDefaultAsync(ct);

            if (ownerId is null)
            {
                throw new NotFoundException($"Trip with ID '{tripId}' was not found.");
            }

            EnsureCanModify(currentUserId, ownerId.Value, "Trip", tripId);
        }

        public async Task EnsureCanModifyTripSegmentAsync(
            Guid currentUserId,
            Guid segmentId,
            CancellationToken ct = default)
        {
            var ownerId = await _context.TripSegments
                .AsNoTracking()
                .Where(segment => segment.Id == segmentId)
                .Select(segment => (Guid?)segment.Trip.UserId)
                .FirstOrDefaultAsync(ct);

            if (ownerId is null)
            {
                throw new NotFoundException($"Trip segment with ID '{segmentId}' was not found.");
            }

            EnsureCanModify(currentUserId, ownerId.Value, "Trip segment", segmentId);
        }

        public void EnsureCanModify(
            Guid currentUserId,
            Guid resourceOwnerId,
            string resourceName,
            Guid resourceId)
        {
            // Profile visibility never grants mutation rights to third parties.
            if (currentUserId != resourceOwnerId)
            {
                throw new ForbiddenException(
                    $"{resourceName} with ID '{resourceId}' belongs to another user.");
            }
        }
    }
}
