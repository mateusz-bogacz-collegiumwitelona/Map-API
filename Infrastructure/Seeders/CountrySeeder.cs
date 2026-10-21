using Domain.Const;
using Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO.Converters;
using System.Text.Json;

namespace Infrastructure.Seeders
{
    public class CountrySeeder
    {
        private readonly AppDbContext _context;
        private readonly ILogger<CountrySeeder> _logger;
        private readonly HttpClient _httpClient;

        public CountrySeeder(
            AppDbContext context,
            ILogger<CountrySeeder> logger,
            HttpClient httpClient
        )
        {
            _context = context;
            _logger = logger;
            _httpClient = httpClient;
        }

        public async Task SeedCountriesAsync()
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

                    boundary.SRID = GeoConst.SRID;

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
