using Domain.Enum;

namespace Services.Response.User
{
    public record UserReportListResponse
    {
        public required Guid Id { get; init; }
        public required string Reason { get; init; }
        public required ReportStatusEnum Status { get; init; }
        public required DateTime CreatedAt { get; init; }
        public DateTime? ResolvedAt { get; init; }
    }
}
