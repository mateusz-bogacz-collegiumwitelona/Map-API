namespace Services.Interfaces
{
    /// <summary>
    /// Central place for profile visibility and resource ownership checks.
    /// Read access to another user's profile resources is granted only for public profiles;
    /// mutation access is granted only to the resource owner.
    /// </summary>
    public interface IResourceAuthorizationService
    {
        Task<Guid> EnsureCanReadProfileAsync(
            Guid currentUserId,
            Guid? targetUserId = null,
            CancellationToken ct = default);

        Task EnsureCanModifyTripAsync(
            Guid currentUserId,
            Guid tripId,
            CancellationToken ct = default);

        Task EnsureCanModifyTripSegmentAsync(
            Guid currentUserId,
            Guid segmentId,
            CancellationToken ct = default);

        void EnsureCanModify(
            Guid currentUserId,
            Guid resourceOwnerId,
            string resourceName,
            Guid resourceId);
    }
}
