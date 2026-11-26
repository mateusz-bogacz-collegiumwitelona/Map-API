using Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace Services.QueryExtension
{
    internal static class PlaceQueryExtension
    {
        private const string LikeEscapeCharacter = "!";

        internal static IQueryable<Place> ApplySearch(
            this IQueryable<Place> query,
            string search)
        {
            var searchPattern =
                $"{EscapeLikePattern(search.Trim())}%";

            return query
                .Where(place => place.IsCatalogPlace)
                .Where(place => EF.Functions.ILike(
                    EF.Functions.Unaccent(place.Name),
                    EF.Functions.Unaccent(searchPattern),
                    LikeEscapeCharacter));
        }

        private static string EscapeLikePattern(string value)
        {
            return value
                .Replace("!", "!!", StringComparison.Ordinal)
                .Replace("%", "!%", StringComparison.Ordinal)
                .Replace("_", "!_", StringComparison.Ordinal);
        }
    }
}