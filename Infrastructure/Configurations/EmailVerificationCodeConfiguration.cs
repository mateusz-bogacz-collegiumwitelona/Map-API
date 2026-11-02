using Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations
{
    public class EmailVerificationCodeConfiguration : IEntityTypeConfiguration<EmailVerificationCode>
    {
        public void Configure(EntityTypeBuilder<EmailVerificationCode> builder)
        {
            builder.HasKey(c => c.Id);

            builder.Property(c => c.Code)
                .HasMaxLength(10)
                .IsRequired();

            builder.Property(c => c.Purpose)
                .HasMaxLength(32)
                .IsRequired();

            builder.Property(c => c.NewEmail)
                .HasMaxLength(256);

            builder.HasOne(c => c.User)
                .WithMany()
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(c => new { c.UserId, c.Code });
            builder.HasIndex(c => new { c.UserId, c.Purpose });
        }
    }
}
