using Domain.Const;
using Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;

namespace Infrastructure.Seeders
{
    public class PlaceSeeder
    {
        private readonly AppDbContext _context;
        private readonly ILogger<PlaceSeeder> _logger;

        public PlaceSeeder(AppDbContext context, ILogger<PlaceSeeder> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task SeedPlacesAsync()
        {
            if (await _context.Places.AnyAsync())
            {
                _logger.LogInformation("Places already exist. Skipping place seeding.");
                return;
            }

            _logger.LogInformation("Seeding travel destinations...");

            var countryMap = await _context.Countries
                .Select(c => new { c.Code, c.Id })
                .ToDictionaryAsync(c => c.Code, c => c.Id);

            string[] requiredCodes = { "PL", "DE", "ES", "CZ" };
            if (!requiredCodes.All(code => countryMap.ContainsKey(code)))
            {
                _logger.LogError("Required countries (PL, DE, ES, CZ) not found in database.");
                return;
            }

            Place CreatePlace(string name, string countryCode, double lng, double lat) => new()
            {
                Name = name,
                CountryId = countryMap[countryCode],
                Location = new Point(lng, lat) { SRID = GeoConst.SRID }
            };

            var places = new List<Place>
            {
                // PL
                CreatePlace("Legnica", "PL", 16.16, 51.21),
                CreatePlace("Wrocław", "PL", 17.03, 51.10),
                CreatePlace("Warszawa", "PL", 21.01, 52.23),
                CreatePlace("Kraków", "PL", 19.94, 50.06),
                CreatePlace("Gdańsk", "PL", 18.64, 54.35),
                CreatePlace("Zakopane", "PL", 19.95, 49.29),

                // DE 
                CreatePlace("Berlin", "DE", 13.40, 52.52),
                CreatePlace("Drezno", "DE", 13.74, 51.05),
                CreatePlace("Monachium", "DE", 11.58, 48.14),
                CreatePlace("Hamburg", "DE", 9.99, 53.55),

                
                // ES 
                CreatePlace("Palma de Mallorca", "ES", 2.65, 39.56),
                CreatePlace("Alcudia", "ES", 3.12, 39.85),
                CreatePlace("Sóller", "ES", 2.71, 39.76),
                CreatePlace("Valldemossa", "ES", 2.62, 39.71),
                CreatePlace("Barcelona", "ES", 2.17, 41.38),
                CreatePlace("Madryt", "ES", -3.70, 40.41),

                // CZ 
                CreatePlace("Praga", "CZ", 14.43, 50.07),
                CreatePlace("Skalne Miasto Adršpach", "CZ", 16.12, 50.61),
                CreatePlace("Brno", "CZ", 16.61, 49.19)
            };

            if (countryMap.TryGetValue("IT", out var itId))
            {
                places.Add(new Place { Name = "Rzym", CountryId = itId, Location = new Point(12.49, 41.90) { SRID = GeoConst.SRID } });
                places.Add(new Place { Name = "Wenecja", CountryId = itId, Location = new Point(12.33, 45.44) { SRID = GeoConst.SRID } });
            }

            if (countryMap.TryGetValue("FR", out var frId))
            {
                places.Add(new Place { Name = "Paryż", CountryId = frId, Location = new Point(2.35, 48.85) { SRID = GeoConst.SRID } });
                places.Add(new Place { Name = "Nicea", CountryId = frId, Location = new Point(7.26, 43.71) { SRID = GeoConst.SRID } });
            }

            await _context.Places.AddRangeAsync(places);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Successfully seeded {Count} places.", places.Count);
        }
    }
}