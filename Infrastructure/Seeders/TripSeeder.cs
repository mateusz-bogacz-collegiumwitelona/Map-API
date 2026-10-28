using Domain.Const;
using Domain.Enum;
using Domain.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;

namespace Infrastructure.Seeders
{
    public class TripSeeder
    {
        private readonly AppDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly ILogger<TripSeeder> _logger;

        public TripSeeder(
            AppDbContext context,
            UserManager<User> userManager,
            ILogger<TripSeeder> logger)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
        }

        public async Task SeedTripsAsync()
        {
            if (await _context.Trips.AnyAsync())
            {
                _logger.LogInformation("Trips already exist. Skipping trip seeding.");
                return;
            }

            var places = await _context.Places.Include(p => p.Country).ToDictionaryAsync(p => p.Name);
            var allPlacesList = places.Values.ToList();

            if (allPlacesList.Count < 2)
            {
                _logger.LogWarning("Cannot seed trips: at least 2 places are required, but found {Count}.", allPlacesList.Count);
                return;
            }

            _logger.LogInformation("Seeding trips and segments...");

            LineString CreateLine(Point p1, Point p2) =>
                new LineString(new[] { p1.Coordinate, p2.Coordinate }) { SRID = GeoConst.SRID };

            var publicUser = await _userManager.FindByEmailAsync("public@example.pl");
            if (publicUser != null)
            {
                await SeedCanonicalTripsForPublicUserAsync(publicUser, places, CreateLine);
            }

            var regularUsers = await _userManager.Users
                .Where(u => u.Email != "admin@example.pl" && u.Email != "public@example.pl")
                .ToListAsync();

            var random = new Random(42);

            foreach (var user in regularUsers)
            {
                await SeedRandomTripsForUserAsync(user, allPlacesList, CreateLine, random);
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("Successfully seeded trips!");
        }


        private async Task SeedCanonicalTripsForPublicUserAsync(
            User user,
            Dictionary<string, Place> places,
            Func<Point, Point, LineString> createLine)
        {
            var trip1 = new Trip
            {
                UserId = user.Id,
                Title = "Wakacje na Majorce (Trasa ze specyfikacji)",
                Description = "Podróż z Legnicy przez Berlin na Majorkę z objazdem wyspy samochodem.",
                StartDate = new DateTime(2024, 7, 10, 0, 0, 0, DateTimeKind.Utc),
                EndDate = new DateTime(2024, 7, 24, 0, 0, 0, DateTimeKind.Utc)
            };
            await _context.Trips.AddAsync(trip1);

            var segments1 = new List<TripSegment>
            {
                new() {
                    Trip = trip1,
                    OrderIndex = 0,
                    TransportType = TransportTypeEnum.Car,
                    OriginPlace = places["Legnica"],
                    DestinationPlace = places["Berlin"],
                    DistanceKm = 230.5m,
                    Geometry = createLine(places["Legnica"].Location, places["Berlin"].Location)
                },
                new() {
                    Trip = trip1,
                    OrderIndex = 1,
                    TransportType = TransportTypeEnum.Plane,
                    OriginPlace = places["Berlin"],
                    DestinationPlace = places["Palma de Mallorca"],
                    DistanceKm = 1650.0m,
                    Geometry = createLine(places["Berlin"].Location, places["Palma de Mallorca"].Location)
                },
                new() {
                    Trip = trip1,
                    OrderIndex = 2,
                    TransportType = TransportTypeEnum.Car,
                    OriginPlace = places["Palma de Mallorca"],
                    DestinationPlace = places["Valldemossa"],
                    DistanceKm = 20.0m,
                    Geometry = createLine(places["Palma de Mallorca"].Location, places["Valldemossa"].Location)
                },
                new() {
                    Trip = trip1,
                    OrderIndex = 3,
                    TransportType = TransportTypeEnum.Car,
                    OriginPlace = places["Valldemossa"],
                    DestinationPlace = places["Sóller"],
                    DistanceKm = 15.0m,
                    Geometry = createLine(places["Valldemossa"].Location, places["Sóller"].Location)
                },
                new() {
                    Trip = trip1,
                    OrderIndex = 4,
                    TransportType = TransportTypeEnum.Car,
                    OriginPlace = places["Sóller"],
                    DestinationPlace = places["Alcudia"],
                    DistanceKm = 52.0m,
                    Geometry = createLine(places["Sóller"].Location, places["Alcudia"].Location)
                },
                new() {
                    Trip = trip1,
                    OrderIndex = 5,
                    TransportType = TransportTypeEnum.Car,
                    OriginPlace = places["Alcudia"],
                    DestinationPlace = places["Palma de Mallorca"],
                    DistanceKm = 54.0m,
                    Geometry = createLine(places["Alcudia"].Location, places["Palma de Mallorca"].Location)
                },
                new() {
                    Trip = trip1,
                    OrderIndex = 6,
                    TransportType = TransportTypeEnum.Plane,
                    OriginPlace = places["Palma de Mallorca"],
                    DestinationPlace = places["Berlin"],
                    DistanceKm = 1650.0m,
                    Geometry = createLine(places["Palma de Mallorca"].Location, places["Berlin"].Location)
                },
                new() {
                    Trip = trip1,
                    OrderIndex = 7,
                    TransportType = TransportTypeEnum.Car,
                    OriginPlace = places["Berlin"],
                    DestinationPlace = places["Legnica"],
                    DistanceKm = 230.5m,
                    Geometry = createLine(places["Berlin"].Location, places["Legnica"].Location)
                }
            };
            await _context.TripSegments.AddRangeAsync(segments1);
            AddVisitedCountries(user, trip1, segments1);

            var trip2 = new Trip
            {
                UserId = user.Id,
                Title = "Weekend w Pradze i Adršpach",
                Description = "Wycieczka koleją i piesze szlaki w skalnym mieście.",
                StartDate = new DateTime(2025, 5, 1, 0, 0, 0, DateTimeKind.Utc),
                EndDate = new DateTime(2025, 5, 4, 0, 0, 0, DateTimeKind.Utc)
            };
            await _context.Trips.AddAsync(trip2);

            var segments2 = new List<TripSegment>
            {
                new() {
                    Trip = trip2,
                    OrderIndex = 0,
                    TransportType = TransportTypeEnum.Train,
                    OriginPlace = places["Wrocław"],
                    DestinationPlace = places["Praga"],
                    DistanceKm = 280.0m,
                    Geometry = createLine(
                        places["Wrocław"].Location,
                        places["Praga"].Location
                        )
                },
                new() { Trip = trip2,
                    OrderIndex = 1,
                    TransportType = TransportTypeEnum.Other,
                    OriginPlace = places["Praga"],
                    DestinationPlace = places["Skalne Miasto Adršpach"],
                    DistanceKm = 145.0m,
                    Geometry = createLine(
                        places["Praga"].Location,
                        places["Skalne Miasto Adršpach"].Location
                        )
                },
                new() {
                    Trip = trip2,
                    OrderIndex = 2,
                    TransportType = TransportTypeEnum.Train,
                    OriginPlace = places["Skalne Miasto Adršpach"],
                    DestinationPlace = places["Wrocław"],
                    DistanceKm = 110.0m,
                    Geometry = createLine(
                        places["Skalne Miasto Adršpach"].Location,
                        places["Wrocław"].Location
                        )
                }
            };
            await _context.TripSegments.AddRangeAsync(segments2);
            AddVisitedCountries(user, trip2, segments2);

            var trip3 = new Trip
            {
                UserId = user.Id,
                Title = "Zimowy wypad do Zakopanego",
                Description = "Odpoczynek w Tatrach.",
                StartDate = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc),
                EndDate = new DateTime(2026, 1, 20, 0, 0, 0, DateTimeKind.Utc)
            };
            await _context.Trips.AddAsync(trip3);

            var segments3 = new List<TripSegment>
            {
                new() {
                    Trip = trip3,
                    OrderIndex = 0,
                    TransportType = TransportTypeEnum.Car,
                    OriginPlace = places["Wrocław"],
                    DestinationPlace = places["Zakopane"],
                    DistanceKm = 360.0m,
                    Geometry = createLine(
                        places["Wrocław"].Location,
                        places["Zakopane"].Location
                        )
                }
            };
            await _context.TripSegments.AddRangeAsync(segments3);
            AddVisitedCountries(user, trip3, segments3);
        }

        private async Task SeedRandomTripsForUserAsync(
            User user,
            List<Place> allPlaces,
            Func<Point, Point, LineString> createLine,
            Random rnd)
        {
            if (allPlaces.Count < 2) return;

            string[] tripThemes = {
                "Urlop objazdowy",
                "Weekendowa ucieczka",
                "Europejskie wakacje",
                "Wyprawa fotograficzna",
                "Wypad ze znajomymi"
            };

            for (int t = 0; t < 3; t++)
            {
                int year = 2024 + t;
                int month = rnd.Next(1, 13);
                int day = rnd.Next(1, 25);
                var startDate = new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Utc);
                int tripDurationDays = rnd.Next(3, 12);

                int currentIndex = rnd.Next(allPlaces.Count);
                var currentPlace = allPlaces[currentIndex];

                var trip = new Trip
                {
                    UserId = user.Id,
                    Title = $"{tripThemes[rnd.Next(tripThemes.Length)]} ({currentPlace.Name})",
                    Description = $"Podróż użytkownika {user.FirstName} po Europie.",
                    StartDate = startDate,
                    EndDate = startDate.AddDays(tripDurationDays)
                };
                await _context.Trips.AddAsync(trip);

                int legsCount = rnd.Next(2, 5);
                var segments = new List<TripSegment>();

                for (int order = 0; order < legsCount; order++)
                {
                    int offset = rnd.Next(1, allPlaces.Count);
                    int nextIndex = (currentIndex + offset) % allPlaces.Count;
                    var nextPlace = allPlaces[nextIndex];

                    var transport = (TransportTypeEnum)rnd.Next(4);
                    decimal distance = CalculateDistanceKm(currentPlace.Location, nextPlace.Location);

                    segments.Add(new TripSegment
                    {
                        Trip = trip,
                        OrderIndex = order,
                        TransportType = transport,
                        OriginPlace = currentPlace,
                        DestinationPlace = nextPlace,
                        DistanceKm = distance,
                        Geometry = createLine(currentPlace.Location, nextPlace.Location),
                        IsFallbackRoute = rnd.Next(10) == 0
                    });

                    currentIndex = nextIndex;
                    currentPlace = nextPlace;
                }

                await _context.TripSegments.AddRangeAsync(segments);
                AddVisitedCountries(user, trip, segments);
            }
        }

        private void AddVisitedCountries(User user, Trip trip, IEnumerable<TripSegment> segments)
        {
            var distinctCountryIds = segments
                .SelectMany(s => new[] { s.OriginPlace.CountryId, s.DestinationPlace.CountryId })
                .Distinct();

            foreach (var countryId in distinctCountryIds)
            {
                _context.VisitedCountries.Add(new VisitedCountry
                {
                    UserId = user.Id,
                    Trip = trip,
                    CountryId = countryId,
                    Year = trip.StartDate.Year
                });
            }
        }

        private static decimal CalculateDistanceKm(Point p1, Point p2)
        {
            const double earthRadiusKm = 6371.0;
            double dLat = (p2.Y - p1.Y) * Math.PI / 180.0;
            double dLon = (p2.X - p1.X) * Math.PI / 180.0;

            double lat1 = p1.Y * Math.PI / 180.0;
            double lat2 = p2.Y * Math.PI / 180.0;

            double a = Math.Sin(dLat / 2.0) * Math.Sin(dLat / 2.0) +
                       Math.Cos(lat1) * Math.Cos(lat2) *
                       Math.Sin(dLon / 2.0) * Math.Sin(dLon / 2.0);

            double c = 2.0 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1.0 - a));
            return Math.Round((decimal)(earthRadiusKm * c), 1);
        }
    }
}