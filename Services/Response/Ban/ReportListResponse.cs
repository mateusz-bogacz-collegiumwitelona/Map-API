using Services.Response.User;

namespace Services.Response.Ban
{
    public sealed record ReportListResponse : UserReportListRespone
    {
        public required Guid ReportedUserId { get; init; }
        public required string ReportedFirstName { get; init; }
        public required string ReportedLastName { get; init; }
    }
}
