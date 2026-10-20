using Domain.Models.Base;

namespace Domain.Models
{
    public class VisitedCountry : BaseEntity
    {
        public Guid UserId { get; set; }
        public User User { get; set; } = null!;

        public Guid TripId { get; set; }
        public Trip Trip { get; set; } = null!;

        public Guid CountryId { get; set; }
        public Country Country { get; set; } = null!;

        public int Year { get; set; }
    }
}
