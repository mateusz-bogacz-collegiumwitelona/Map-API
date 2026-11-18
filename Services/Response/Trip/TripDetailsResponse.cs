using Domain.Enum;

namespace Services.Response.Trip
{
    public sealed record TripDetailsResponse
    {
        public required Guid Id { get; init; }
        public required string Title { get; init; }
        public string? Description { get; init; }
        public required DateTime StartDate { get; init; }
        public DateTime? EndDate { get; init; }
        public required List<TripDetailsSegmentResponse> Segments { get; init; }
    }
}