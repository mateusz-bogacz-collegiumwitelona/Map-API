using Domain.Models;
using Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Api.Configuration
{
    public static class DatabaseMigrationConfiguration
    {
        public static async Task ApplyMigrationsAsync(this WebApplication app)
        {
            using var scope = app.Services.CreateScope();
            var services = scope.ServiceProvider;

            var logger = services.GetRequiredService<ILogger<Program>>();

            try
            {
                var dbContext = services.GetRequiredService<AppDbContext>();
                await dbContext.Database.MigrateAsync();

                var roleManager = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
                var userManager = services.GetRequiredService<UserManager<User>>();

            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred during database migration or seeding.");
                throw;
            }
        }
    }
}