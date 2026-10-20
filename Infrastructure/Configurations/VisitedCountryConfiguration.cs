using Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations
{
    public class VisitedCountryConfiguration : IEntityTypeConfiguration<VisitedCountry>
    {
        public void Configure(EntityTypeBuilder<VisitedCountry> builder)
        {
            builder.HasKey(vc => vc.Id);

            builder.HasOne(vc => vc.User)
                .WithMany()
                .HasForeignKey(vc => vc.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(vc => vc.Trip)
                .WithMany()
                .HasForeignKey(vc => vc.TripId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(vc => vc.Country)
                .WithMany()
                .HasForeignKey(vc => vc.CountryId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(vc => new { vc.UserId, vc.Year });
        }
    }
}
