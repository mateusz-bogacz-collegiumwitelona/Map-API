namespace Api.Request.Trip
{
    public record CoordinatesRequest
    {
        public required double Latitude { get; init; }
        public required double Longitude { get; init; }
    }
}