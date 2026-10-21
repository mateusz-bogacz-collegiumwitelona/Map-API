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
            ILogger<TripSeeder> logger
        )
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

            _logger.LogInformation("Seeding trips (canonical for public, randomized for others, none for admin)...");

            var places = await _context.Places.Include(p => p.Country).ToDictionaryAsync(p => p.Name);
            var allPlacesList = places.Values.ToList();
            var users = await _userManager.Users.ToListAsync();

            LineString CreateLine(Point p1, Point p2) =>
                new LineString(new[] { p1.Coordinate, p2.Coordinate }) { SRID = GeoConst.SRID };

            var random = new Random(42);

            foreach (var user in users)
            {
                if (user.Email == "admin@example.pl")
                {
                    continue;
                }

                if (user.Email == "public@example.pl")
                {
                    await SeedCanonicalTripsForPublicUserAsync(user, places, CreateLine);
                }
                else
                {
                    await SeedRandomTripsForUserAsync(user, allPlacesList, CreateLine, random);
                }
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("Successfully seeded trips!");
        }

        private async Task SeedCanonicalTripsForPublicUserAsync(
            User user, Dictionary<string, Place> places, Func<Point, Point, LineString> createLine)
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
                    Geometry = createLine(
                        places["Legnica"].Location,
                        places["Berlin"].Location
                        )
                },
                new() {
                    Trip = trip1,
                    OrderIndex = 1,
                    TransportType = TransportTypeEnum.Plane,
                    OriginPlace = places["Berlin"],
                    DestinationPlace = places["Palma de Mallorca"],
                    DistanceKm = 1650.0m,
                    Geometry = createLine(
                        places["Berlin"].Location,
                        places["Palma de Mallorca"].Location
                        )
                },
                new() {
                    Trip = trip1,
                    OrderIndex = 2,
                    TransportType = TransportTypeEnum.Car,
                    OriginPlace = places["Palma de Mallorca"],
                    DestinationPlace = places["Valldemossa"],
                    DistanceKm = 20.0m,
                    Geometry = createLine(
                        places["Palma de Mallorca"].Location,
                        places["Valldemossa"].Location
                        )
                },
                new() {
                    Trip = trip1,
                    OrderIndex = 3,
                    TransportType = TransportTypeEnum.Car,
                    OriginPlace = places["Valldemossa"],
                    DestinationPlace = places["Sóller"],
                    DistanceKm = 15.0m,
                    Geometry = createLine(
                        places["Valldemossa"].Location,
                        places["Sóller"].Location
                        )
                },
                new() {
                    Trip = trip1,
                    OrderIndex = 4,
                    TransportType = TransportTypeEnum.Car,
                    OriginPlace = places["Sóller"],
                    DestinationPlace = places["Alcudia"],
                    DistanceKm = 52.0m,
                    Geometry = createLine(
                        places["Sóller"].Location,
                        places["Alcudia"].Location
                        )
                },
                new() {
                    Trip = trip1,
                    OrderIndex = 5,
                    TransportType = TransportTypeEnum.Car,
                    OriginPlace = places["Alcudia"],
                    DestinationPlace = places["Palma de Mallorca"],
                    DistanceKm = 54.0m,
                    Geometry = createLine(
                        places["Alcudia"].Location,
                        places["Palma de Mallorca"].Location
                        )
                },
                new() {
                    Trip = trip1,
                    OrderIndex = 6,
                    TransportType = TransportTypeEnum.Plane,
                    OriginPlace = places["Palma de Mallorca"],
                    DestinationPlace = places["Berlin"],
                    DistanceKm = 1650.0m,
                    Geometry = createLine(
                        places["Palma de Mallorca"].Location,
                        places["Berlin"].Location
                        )
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
                new()
                {
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
                new()
                {
                    Trip = trip2,
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
                    Trip = trip3, OrderIndex = 0,
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
            User user, List<Place> allPlaces, Func<Point, Point, LineString> createLine, Random rnd)
        {
            string[] tripThemes = { "Urlop objazdowy", "Weekendowa ucieczka", "Europejskie wakacje", "Wyprawa fotograficzna", "Wypad ze znajomymi" };

            for (int t = 0; t < 3; t++)
            {
                int year = 2024 + t;
                int month = rnd.Next(1, 13);
                int day = rnd.Next(1, 25);
                var startDate = new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Utc);
                int tripDurationDays = rnd.Next(3, 12);

                var currentPlace = allPlaces[rnd.Next(allPlaces.Count)];
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
                    Place nextPlace;
                    do
                    {
                        nextPlace = allPlaces[rnd.Next(allPlaces.Count)];
                    } while (nextPlace.Id == currentPlace.Id);

                    var transport = (TransportTypeEnum)rnd.Next(4);
                    decimal distance = Math.Round((decimal)Math.Max(35.0, currentPlace.Location.Distance(nextPlace.Location) * 90.0), 1);

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
    }
}
