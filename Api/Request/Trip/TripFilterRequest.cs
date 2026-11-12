using Microsoft.AspNetCore.Mvc;

namespace Api.Request.Trip
{
    public sealed record TripFilterRequest
    {
        [FromQuery(Name = "userId")]
        public Guid? UserId { get; init; }

        [FromQuery(Name = "year")]
        public int? Year { get; init; }
    }
}
