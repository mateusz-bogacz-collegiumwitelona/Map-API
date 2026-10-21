using Domain.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Seeders
{
    public class UserSeeder
    {
        private readonly RoleManager<IdentityRole<Guid>> _roleManager;
        private readonly UserManager<User> _userManager;
        private readonly ILogger<UserSeeder> _logger;
        private readonly IConfiguration _configuration;

        public UserSeeder(
             RoleManager<IdentityRole<Guid>> roleManager,
             UserManager<User> userManager,
             ILogger<UserSeeder> logger,
             IConfiguration configuration)
        {
            _roleManager = roleManager;
            _userManager = userManager;
            _logger = logger;
            _configuration = configuration;
        }

        public async Task SeedAsync()
        {
            await SeedRoleAsync();
            await SeedUserAsync();
        }

        private async Task SeedRoleAsync()
        {
            try
            {
                if (await _roleManager.Roles.AnyAsync()) return;

                string[] roleNames = { "Admin", "User" };

                foreach (var roleName in roleNames)
                {
                    if (!await _roleManager.RoleExistsAsync(roleName))
                    {
                        var result = await _roleManager.CreateAsync(new IdentityRole<Guid>
                        {
                            Name = roleName,
                            NormalizedName = roleName.ToUpper()
                        });

                        if (!result.Succeeded)
                        {
                            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                            throw new InvalidOperationException($"Failed to create role '{roleName}': {errors}");
                        }

                        _logger.LogInformation("Role '{RoleName}' created successfully.", roleName);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while seeding roles.");
                throw;
            }
        }

        private async Task SeedUserAsync()
        {
            try
            {
                if (!await _roleManager.RoleExistsAsync("Admin") ||
                        !await _roleManager.RoleExistsAsync("Manager") ||
                        !await _roleManager.RoleExistsAsync("User"))
                {
                    await SeedRoleAsync();
                }

                string adminEmail = _configuration["SEED_ADMIN_EMAIL"]
                    ?? throw new InvalidDataException("SEED_ADMIN_EMAIL is not set in the configuration.");
                string adminPassword = _configuration["SEED_ADMIN_PASSWORD"]
                    ?? throw new InvalidDataException("SEED_ADMIN_PASSWORD is not set in the configuration.");

                // admin
                await CreateUserAsync(
                    email: adminEmail,
                    password: adminPassword,
                    role: "Admin",
                    firstName: "Tomasz",
                    lastName: "Kot",
                    bio: null,
                    isPublic: false
                );

                // public
                await CreateUserAsync(
                    email: "public@example.pl",
                    password: "Public123!",
                    role: "User",
                    firstName: "Adam",
                    lastName: "Kowalki",
                    bio: "Test public account",
                    isPublic: true
                );

                // private
                await CreateUserAsync(
                    email: "private@example.pl",
                    password: "Private123!",
                    role: "User",
                    firstName: "Jan",
                    lastName: "Urban",
                    bio: "Test private account",
                    isPublic: false
                );

                for (int i = 0; i < 3; i++)
                {
                    await CreateUserAsync(
                        email: $"Test{i}@example.pl",
                        password: $"TestUser{i}!123",
                        role: "User",
                        firstName: $"Test{i}",
                        lastName: $"Test{i}",
                        bio: "Test private account",
                        isPublic: true
                    );
                }

                _logger.LogInformation("User seeding completed successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while seeding users.");

            }
        }

        private async Task CreateUserAsync(
            string email,
            string password,
            string role,
            string firstName,
            string lastName,
            string? bio,
            bool isPublic)
        {
            bool isUserExist = await _userManager.FindByEmailAsync(email) != null;

            if (!isUserExist)
            {
                try
                {
                    User newUser = new User
                    {
                        UserName = email,
                        Email = email,
                        FirstName = firstName,
                        LastName = lastName,
                        Bio = bio,
                        IsPublic = isPublic,
                        EmailConfirmed = true,
                    };

                    var createResult = await _userManager.CreateAsync(newUser, password);

                    if (!createResult.Succeeded)
                    {
                        var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
                        _logger.LogError("Failed to create user '{Email}': {Errors}", email, errors);
                        throw new InvalidOperationException($"Could not seed required user '{email}': {errors}");
                    }

                    var roleResult = await _userManager.AddToRoleAsync(newUser, role);
                    if (!roleResult.Succeeded)
                    {
                        var errors = string.Join(", ", roleResult.Errors.Select(e => e.Description));
                        _logger.LogError("Failed to assign role '{Role}' to user '{Email}': {Errors}", role, email, errors);
                        throw new InvalidOperationException($"Could not assign role '{role}' to user '{email}': {errors}");
                    }

                    _logger.LogInformation("User {FirstName} {LastName} created and assigned to role '{Role}'.", firstName, lastName, role);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred while seeding user '{firstName} {lastName}'.", firstName, lastName);
                }
            }
        }
    }
}

