using Services.Response.User;

namespace Services.Response.Ban
{
    public sealed record ReportListResponse : UserReportListResponse
    {
        public required Guid ReportedUserId { get; init; }
        public required string FirstName { get; init; }
        public required string LastName { get; init; }
    }
}
