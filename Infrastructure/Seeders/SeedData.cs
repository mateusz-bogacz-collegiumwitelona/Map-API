using Domain.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Seeders
{
    public class SeedData
    {
        private readonly AppDbContext _context;
        private readonly ILogger<SeedData> _logger;
        private readonly RoleManager<IdentityRole<Guid>> _roleManager;
        private readonly UserManager<User> _userManager;

        public SeedData(
            AppDbContext context,
            ILogger<SeedData> logger,
            RoleManager<IdentityRole<Guid>> roleManager,
            UserManager<User> userManager
            )
        {
            _context = context;
            _logger = logger;
            _roleManager = roleManager;
            _userManager = userManager;
        }

        public async Task InitAsync()
        {
            if (!await _roleManager.Roles.AnyAsync()) await SeedRoleAsync();
            if (!await _userManager.Users.AnyAsync()) await SeedUserAsync();
        }

        private async Task SeedRoleAsync()
        {
            try
            {
                string[] roleNames = { "Admin", "User" };

                foreach (var roleName in roleNames)
                {
                    if (!await _roleManager.RoleExistsAsync(roleName))
                    {
                        await _roleManager.CreateAsync(new IdentityRole<Guid>
                        {
                            Name = roleName,
                            NormalizedName = roleName.ToUpper()
                        });

                        Console.WriteLine($"Role '{roleName}' created successfully.");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while seeding roles.");
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

                await CreateUserAsync(
                    email: "public@example.pl",
                    password: "Public123!",
                    role: "User",
                    firstName: "Adam",
                    lastName: "Kowalki",
                    bio: "Test public account",
                    isPublic: true
                    );

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
                    };

                    var result = await _userManager.CreateAsync(newUser, password);

                    if (result.Succeeded)
                    {
                        await _userManager.AddToRoleAsync(newUser, role);
                        _logger.LogInformation("User {firstName} {lastName} created successfully.", firstName, lastName);
                    }
                    else
                    {
                        var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                        _logger.LogWarning("Failed to create user '{email}': {errors}", email, errors);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred while seeding user '{firstName} {lastName}'.", firstName, lastName);
                }
            }
        }
    }
}
