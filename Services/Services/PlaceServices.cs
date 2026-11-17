using Domain.Common;
using Domain.Models;
using Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Services.Accessors.Interfaces;
using Services.Interfaces;
using Services.Response.Place;

namespace Services.Services
{
    public sealed class PlaceServices : IPlaceServices
    {
        private const int MaxResults = 10;

        private readonly AppDbContext _context;
        private readonly ILogger<PlaceServices> _logger;
        private readonly ICancellationTokenAccessor _ctAccessor;

        public PlaceServices(
            AppDbContext context,
            ILogger<PlaceServices> logger,
            ICancellationTokenAccessor ctAccessor)
        {
            _context = context;
            _logger = logger;
            _ctAccessor = ctAccessor;
        }

        public async Task<Result<List<PlaceSearchResponse>>> SearchPlacesAsync(
            string query)
        {
            if (string.IsNullOrWhiteSpace(query) ||
                query.Trim().Length < 2 ||
                query.Trim().Length > 100)
            {
                throw new ArgumentException(
                    "Search query must contain between 2 and 100 characters.");
            }

            var searchPattern = $"{query.Trim()}%";

            var places = await _context.Places
                .AsNoTracking()
                .Where(place => EF.Functions.ILike(place.Name, searchPattern))
                .OrderBy(place => place.Name)
                .ThenBy(place => place.Country.Name)
                .Take(MaxResults)
                .Select(place => new PlaceSearchResponse
                {
                    Id = place.Id,
                    Name = place.Name,
                    CountryName = place.Country.Name,
                    CountryCode = place.Country.Code,
                    Latitude = place.Location.Y,
                    Longitude = place.Location.X
                })
                .ToListAsync(_ctAccessor.Token);

            _logger.LogInformation(
                "Found {Count} places matching query {Query}",
                places.Count,
                query);

            return Result<List<PlaceSearchResponse>>.Success(
                message: "Place search completed successfully.",
                statusCode: StatusCodes.Status200OK,
                data: places);
        }
    }
}