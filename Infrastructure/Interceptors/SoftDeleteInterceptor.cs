using Domain.Models.Base.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Infrastructure.Interceptors
{
    public class SoftDeleteInterceptor : SaveChangesInterceptor
    {
        public override InterceptionResult<int> SavingChanges(
            DbContextEventData eventData,
            InterceptionResult<int> result)
        {
            ApplyAuditAndSoftDelete(eventData.Context);
            return base.SavingChanges(eventData, result);
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            ApplyAuditAndSoftDelete(eventData.Context);
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        private static void ApplyAuditAndSoftDelete(DbContext? context)
        {
            if (context is null) return;

            var now = DateTime.UtcNow;

            foreach (var entry in context.ChangeTracker.Entries<ISoftDeletableEntity>().ToList())
            {
                if (entry.State == EntityState.Deleted)
                {
                    entry.State = EntityState.Modified;
                    entry.Entity.IsDeleted = true;
                    entry.Entity.DeletedAt = now;
                }
            }

            foreach (var entry in context.ChangeTracker.Entries<IAuditableEntity>().ToList())
            {
                switch (entry.State)
                {
                    case EntityState.Added:
                        entry.Entity.CreatedAt = now;
                        entry.Entity.UpdatedAt = now;
                        break;

                    case EntityState.Modified:
                        entry.Entity.UpdatedAt = now;
                        break;
                }
            }
        }
    }
}