using Domain.Enum;

namespace Services.Response.Ban
{
    public sealed record ReportDetailResponse
    {
        public required Guid Id { get; init; }

        public required string ReportedFirstName { get; init; }
        public required string ReportedLastName { get; init; }

        public required string ReporterFirstName { get; init; }
        public required string ReporterLastName { get; init; }

        public required string Reason { get; init; }

        public ReportStatusEnum Status { get; init; }

        public string? ResolvedByAdminFirstName { get; init; }
        public string? ResolvedByAdminLastName { get; init; }

        public DateTime? ResolvedAt { get; init; }

        public required DateTime CreatedAt { get; init; }
    }
}
