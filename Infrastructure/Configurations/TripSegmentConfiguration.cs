using Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Text;

namespace Infrastructure.Configurations
{
    public class TripSegmentConfiguration : IEntityTypeConfiguration<TripSegment>
    {
        public void Configure(EntityTypeBuilder<TripSegment> builder)
        {
            builder.HasKey(s => s.Id);
            builder.Property(s => s.DistanceKm).HasPrecision(10, 2);

            builder.Property(s => s.Geometry)
                .HasColumnType("geometry(LineString, 4326)")
                .IsRequired();

            builder.HasIndex(s => s.Geometry).HasMethod("gist");

            builder.HasOne(s => s.Trip)
                .WithMany()
                .HasForeignKey(s => s.TripId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(s => s.OriginPlace)
                .WithMany()
                .HasForeignKey(s => s.OriginPlaceId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(s => s.DestinationPlace)
                .WithMany()
                .HasForeignKey(s => s.DestinationPlaceId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(s => new { s.TripId, s.OrderIndex });
        }
    }
}
