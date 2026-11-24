using Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations
{
    public class UserConfiguration : IEntityTypeConfiguration<User>
    {
        public void Configure(EntityTypeBuilder<User> builder)
        {
            builder.HasIndex(u => u.NormalizedEmail)
                .HasDatabaseName("EmailIndex")
                .IsUnique();

            builder.HasIndex(u => new { u.IsPublic, u.IsDeleted, u.FirstName, u.LastName })
                .HasDatabaseName("IX_Users_PublicActive_Name");

            builder.HasIndex(u => new { u.IsPublic, u.IsDeleted, u.CreatedAt })
                .HasDatabaseName("IX_Users_PublicActive_CreatedAt");
        }
    }
}
