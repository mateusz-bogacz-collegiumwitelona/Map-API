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
            HttpClient httpClient)
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

            try
            {
                var localFilePath = Path.Combine(AppContext.BaseDirectory, "Seeders", "Data", "countries.geojson");
                Stream jsonStream;

                if (File.Exists(localFilePath))
                {
                    _logger.LogInformation("Loading countries GeoJSON from local file: {Path}", localFilePath);
                    jsonStream = File.OpenRead(localFilePath);
                }
                else
                {
                    _logger.LogInformation("Local countries file not found. Downloading from GitHub...");

                    if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
                    {
                        _httpClient.DefaultRequestHeaders.Add("User-Agent", "TravelApp-Seeder");
                    }

                    const string countriesUrl = "https://raw.githubusercontent.com/datasets/geo-countries/master/data/countries.geojson";
                    jsonStream = await _httpClient.GetStreamAsync(countriesUrl);
                }

                using (jsonStream)
                {
                    var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    jsonOptions.Converters.Add(new GeoJsonConverterFactory());

                    var featureCollection = await JsonSerializer.DeserializeAsync<FeatureCollection>(jsonStream, jsonOptions);

                    if (featureCollection == null || featureCollection.Count == 0)
                    {
                        throw new InvalidOperationException("Failed to seed countries: GeoJSON data is empty or invalid.");
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
                                if (!string.IsNullOrWhiteSpace(val) && val != "-99") return val;
                            }
                        }
                        return null;
                    }

                    foreach (var feature in featureCollection)
                    {
                        string? code = GetAttribute(feature.Attributes, "ISO3166-1-Alpha-2", "ISO_A2", "ISO2");

                        if (string.IsNullOrWhiteSpace(code)) continue;

                        string normalizedCode = code.Trim().ToUpper();

                        if (normalizedCode.Length != 2) continue;

                        MultiPolygon? boundary = feature.Geometry switch
                        {
                            MultiPolygon mp => mp,
                            Polygon p => new MultiPolygon(new[] { p }),
                            _ => null
                        };

                        if (boundary == null) continue;
                        boundary.SRID = GeoConst.SRID;

                        var existing = countriesToAdd.FirstOrDefault(c => c.Code == normalizedCode);
                        if (existing != null)
                        {
                            var combined = existing.Boundary.Union(boundary);
                            existing.Boundary = combined switch
                            {
                                MultiPolygon mp => mp,
                                Polygon p => new MultiPolygon(new[] { p }),
                                _ => existing.Boundary
                            };
                            existing.Boundary.SRID = GeoConst.SRID;
                            continue; 
                        }

                        string name = GetAttribute(feature.Attributes, "name", "ADMIN", "NAME", "NAME_EN") ?? "Unknown";

                        countriesToAdd.Add(new Country
                        {
                            Name = name,
                            Code = normalizedCode,
                            Boundary = boundary
                        });
                    }

                    if (countriesToAdd.Count == 0)
                    {
                        throw new InvalidOperationException("Failed to seed countries: No valid 2-letter ISO countries could be processed.");
                    }

                    await _context.Countries.AddRangeAsync(countriesToAdd);
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Successfully seeded {Count} countries with unified 2-letter ISO codes!", countriesToAdd.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while seeding countries.");
                throw;
            }
        }
    }
}