using Domain.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO.Converters;
using System.Text.Json;

namespace Infrastructure.Seeders
{
    public class SeedData
    {
        private readonly AppDbContext _context;
        private readonly ILogger<SeedData> _logger;
        private readonly RoleManager<IdentityRole<Guid>> _roleManager;
        private readonly UserManager<User> _userManager;
        private readonly IHostEnvironment _environment;
        private readonly HttpClient _httpClient;

        private const int SRID = 4326;

        public SeedData(
            AppDbContext context,
            ILogger<SeedData> logger,
            RoleManager<IdentityRole<Guid>> roleManager,
            UserManager<User> userManager,
            IHostEnvironment environment,
            HttpClient httpClient
            )
        {
            _context = context;
            _logger = logger;
            _roleManager = roleManager;
            _userManager = userManager;
            _environment = environment;
            _httpClient = httpClient;
        }

        public async Task InitAsync()
        {
            if (_environment.IsProduction())
            {
                _logger.LogInformation("Production environment detected. Skipping seeder.");
                return;
            }
            await SeedRoleAsync();
            await SeedUserAsync();
            await SeedCountriesAsync();
        }

        private async Task SeedRoleAsync()
        {
            try
            {
                if (await _roleManager.Roles.AnyAsync())
                {
                    _logger.LogInformation("Roles already exist. Skipping role seeding.");
                    return;
                }

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

                await CreateUserAsync(
                   email: "admin@example.pl",
                   password: "Admin123!",
                   role: "Admin",
                   firstName: "Tomasz",
                   lastName: "Kot",
                   bio: null,
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

        private async Task SeedCountriesAsync()
        {
            if (await _context.Countries.AnyAsync())
            {
                _logger.LogInformation("Countries already exist. Skipping country seeding.");
                return;
            }

            const string countriesUrl = "https://raw.githubusercontent.com/datasets/geo-countries/master/data/countries.geojson";

            try
            {
                _logger.LogInformation("Downloading countries GeoJSON...");

                if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
                {
                    _httpClient.DefaultRequestHeaders.Add("User-Agent", "TravelApp-Seeder");
                }

                var response = await _httpClient.GetStreamAsync(countriesUrl);
                var jsonOptions = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                jsonOptions.Converters.Add(new GeoJsonConverterFactory());

                var featureCollection = await JsonSerializer.DeserializeAsync<FeatureCollection>(response, jsonOptions);

                if (featureCollection == null || featureCollection.Count == 0)
                {
                    _logger.LogWarning("No countries found in the GeoJSON data.");
                    return;
                }

                var countriesToAdd = new List<Country>();

                string? GetAttribute(IAttributesTable? attributes, params string[] candidateKeys)
                {
                    if (attributes == null) return null;

                    var names = attributes.GetNames();
                    foreach (var candidate in candidateKeys)
                    {
                        var matchedName = names.FirstOrDefault(n => string.Equals(n, candidate, StringComparison.OrdinalIgnoreCase));
                        if (matchedName != null)
                        {
                            var val = attributes[matchedName]?.ToString()?.Trim();
                            if (!string.IsNullOrWhiteSpace(val) && val != "-99")
                            {
                                return val;
                            }
                        }
                    }
                    return null;
                }

                foreach (var feature in featureCollection)
                {
                    string? code = GetAttribute(feature.Attributes, "ISO3166-1-Alpha-2", "ISO_A2", "ISO2");

                    if (string.IsNullOrWhiteSpace(code) || code.Length > 3)
                    {
                        code = GetAttribute(feature.Attributes, "ISO3166-1-Alpha-3", "ISO_A3", "ISO3", "ADM0_A3");
                    }

                    if (string.IsNullOrWhiteSpace(code)) continue;

                    string normalizedCode = code.Trim().ToUpper();

                    if (normalizedCode.Length < 2 || normalizedCode.Length > 3)
                    {
                        continue;
                    }

                    if (countriesToAdd.Any(c => c.Code == normalizedCode))
                    {
                        continue;
                    }

                    string name = GetAttribute(feature.Attributes, "name", "ADMIN", "NAME", "NAME_EN") ?? "Unknown";

                    MultiPolygon? boundary = feature.Geometry switch
                    {
                        MultiPolygon mp => mp,
                        Polygon p => new MultiPolygon(new[] { p }),
                        _ => null
                    };

                    if (boundary == null) continue;

                    boundary.SRID = SRID;

                    countriesToAdd.Add(new Country
                    {
                        Name = name,
                        Code = normalizedCode,
                        Boundary = boundary
                    });
                }

                if (countriesToAdd.Count == 0 && featureCollection.Count > 0)
                {
                    var sample = featureCollection[0];
                    _logger.LogError("Debug: Parsed 0 countries! First feature attributes: {Attrs}, Geometry: {Geom}",
                        string.Join(", ", sample.Attributes?.GetNames() ?? Array.Empty<string>()),
                        sample.Geometry?.GetType().Name ?? "NULL");
                }

                await _context.Countries.AddRangeAsync(countriesToAdd);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Successfully seeded {Count} countries with real spatial boundaries!", countriesToAdd.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while seeding countries.");
                throw;
            }
        }
    }
}
