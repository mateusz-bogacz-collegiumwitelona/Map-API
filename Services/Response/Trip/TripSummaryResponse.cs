using Domain.Enum;

namespace Services.Response.Trip
{
    public sealed record TripSummaryResponse
    {
        public required Guid Id { get; init; }
        public required string Title { get; init; }
        public string? Description { get; init; }
        public required DateTime StartDate { get; init; }
        public DateTime? EndDate { get; init; }
        public decimal TotalDistance { get; init; } 
        public IReadOnlyList<TransportTypeEnum> TransportTypes { get; init; } = [];
    }
}
