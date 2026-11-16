using Domain.Enum;

namespace Services.Response.Ban
{
    public sealed record ReportListResponse
    {
        public required Guid Id { get; init; }
        public required Guid ReportedUserId { get; init; }
        public required string FirstName { get; init; }
        public required string LastName { get; init; }
        public required string Reason { get; init; }
        public required ReportStatusEnum Status { get; init; }
        public required DateTime CreatedAt { get; init; }
        public DateTime? ResolvedAt { get; init; }
    }
}
