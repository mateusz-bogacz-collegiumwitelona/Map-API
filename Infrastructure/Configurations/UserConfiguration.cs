using Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations
{
    public class UserConfiguration : IEntityTypeConfiguration<User>
    {
        public void Configure(EntityTypeBuilder<User> builder)
        {
            // Identity creates EmailIndex as non-unique; making it unique stops two accounts from sharing an email
            // when requests race past the application-level check.
            builder.HasIndex(u => u.NormalizedEmail)
                .HasDatabaseName("EmailIndex")
                .IsUnique();
        }
    }
}
