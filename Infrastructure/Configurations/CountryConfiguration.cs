using Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations
{
    public class CountryConfiguration : IEntityTypeConfiguration<Country>
    {
        public void Configure(EntityTypeBuilder<Country> builder)
        {
            builder.HasKey(c => c.Id);
            builder.Property(c => c.Name).HasMaxLength(100).IsRequired();
            builder.Property(c => c.Code).HasMaxLength(3).IsRequired();
            builder.HasIndex(c => c.Code).IsUnique();

            builder.Property(c => c.Boundary)
                .HasColumnType("geometry(MultiPolygon, 4326)")
                .IsRequired();

            builder.HasIndex(c => c.Boundary).HasMethod("gist");
        }
    }
}
