using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Seeders
{
    public class InitialSeeder
    {
        private readonly IHostEnvironment _environment;
        private readonly ILogger<InitialSeeder> _logger;

        private readonly UserSeeder _userSeeder;
        private readonly CountrySeeder _countrySeeder;
        private readonly PlaceSeeder _placeSeeder;
        private readonly TripSeeder _tripSeeder;


        public InitialSeeder(
            IHostEnvironment environment,
            ILogger<InitialSeeder> logger,
            UserSeeder userSeeder,
            CountrySeeder countrySeeder,
            PlaceSeeder placeSeeder,
            TripSeeder tripSeeder
            )
        {
            _environment = environment;
            _logger = logger;
            _userSeeder = userSeeder;
            _countrySeeder = countrySeeder;
            _placeSeeder = placeSeeder;
            _tripSeeder = tripSeeder;
        }

        public async Task InitAsync()
        {
            if (_environment.IsProduction())
            {
                _logger.LogInformation("Production environment detected. Skipping seeder.");
                return;
            }
            await _userSeeder.SeedAsync();
            await _countrySeeder.SeedCountriesAsync();
            await _placeSeeder.SeedPlacesAsync();
            await _tripSeeder.SeedTripsAsync();
        }
    }
}
