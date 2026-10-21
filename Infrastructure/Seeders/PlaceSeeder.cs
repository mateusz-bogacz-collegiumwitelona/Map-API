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

            _logger.LogInformation("Seeding extended travel destinations...");


            var countries = await _context.Countries.ToListAsync();

            Country? GetCountry(params string[] codes) =>
                countries.FirstOrDefault(c => codes.Contains(c.Code, StringComparer.OrdinalIgnoreCase));

            var pl = GetCountry("PL", "POL");
            var de = GetCountry("DE", "DEU");
            var es = GetCountry("ES", "ESP");
            var cz = GetCountry("CZ", "CZE");
            var it = GetCountry("IT", "ITA");
            var fr = GetCountry("FR", "FRA");

            if (pl == null || de == null || es == null || cz == null)
            {
                _logger.LogError("Required countries (PL, DE, ES, CZ) not found in database.");
                return;
            }

            Place CreatePlace(string name, Country country, double lng, double lat) => new()
            {
                Name = name,
                CountryId = country.Id,
                Location = new Point(lng, lat) { SRID = GeoConst.SRID }
            };

            var places = new List<Place>
            {
                // PL
                CreatePlace("Legnica", pl, 16.16, 51.21),
                CreatePlace("Wrocław", pl, 17.03, 51.10),
                CreatePlace("Warszawa", pl, 21.01, 52.23),
                CreatePlace("Kraków", pl, 19.94, 50.06),
                CreatePlace("Gdańsk", pl, 18.64, 54.35),
                CreatePlace("Zakopane", pl, 19.95, 49.29),

                // DE
                CreatePlace("Berlin", de, 13.40, 52.52),
                CreatePlace("Drezno", de, 13.74, 51.05),
                CreatePlace("Monachium", de, 11.58, 48.14),
                CreatePlace("Hamburg", de, 9.99, 53.55),

                // ES
                CreatePlace("Palma de Mallorca", es, 2.65, 39.56),
                CreatePlace("Alcudia", es, 3.12, 39.85),
                CreatePlace("Sóller", es, 2.71, 39.76),
                CreatePlace("Valldemossa", es, 2.62, 39.71),
                CreatePlace("Barcelona", es, 2.17, 41.38),
                CreatePlace("Madryt", es, -3.70, 40.41),

                CreatePlace("Praga", cz, 14.43, 50.07),
                CreatePlace("Skalne Miasto Adršpach", cz, 16.12, 50.61),
                CreatePlace("Brno", cz, 16.61, 49.19)
            };

            if (it != null)
            {
                places.Add(CreatePlace("Rzym", it, 12.49, 41.90));
                places.Add(CreatePlace("Wenecja", it, 12.33, 45.44));
            }

            if (fr != null)
            {
                places.Add(CreatePlace("Paryż", fr, 2.35, 48.85));
                places.Add(CreatePlace("Nicea", fr, 7.26, 43.71));
            }

            await _context.Places.AddRangeAsync(places);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Successfully seeded {Count} travel destinations across Europe!", places.Count);
        }
    }
}
