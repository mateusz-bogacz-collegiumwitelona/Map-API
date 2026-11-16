using Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace Services.QueryExtension
{
    internal static class TripQueryExtension
    {
        internal static IQueryable<Trip> ApplySearch(this IQueryable<Trip> query, string? search)
        {
            if (!string.IsNullOrWhiteSpace(search))
            {
                var terms = search.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                foreach (var term in terms)
                {
                    var pattern = $"%{term}%";

                    query = query.Where(t =>
                        EF.Functions.ILike(EF.Functions.Unaccent(t.Title), EF.Functions.Unaccent(pattern)) ||
                        (t.Description != null && EF.Functions.ILike(EF.Functions.Unaccent(t.Description), EF.Functions.Unaccent(pattern))));
                }
            }

            return query;
        }

        public static IQueryable<Trip> ApplySorting(
            this IQueryable<Trip> query,
            string? sortBy,
            bool sortDescending) => sortBy?.ToLowerInvariant() switch
            {
                "startdate" => sortDescending
                    ? query.OrderByDescending(t => t.StartDate).ThenByDescending(t => t.Id)
                    : query.OrderBy(t => t.StartDate).ThenBy(t => t.Id),

                "enddate" => sortDescending
                    ? query.OrderByDescending(t => t.EndDate).ThenByDescending(t => t.Id)
                    : query.OrderBy(t => t.EndDate).ThenBy(t => t.Id),

                "title" => sortDescending
                    ? query.OrderByDescending(t => t.Title).ThenByDescending(t => t.Id)
                    : query.OrderBy(t => t.Title).ThenBy(t => t.Id),

                _ => sortDescending
                    ? query.OrderByDescending(t => t.StartDate).ThenByDescending(t => t.Id)
                    : query.OrderBy(t => t.StartDate).ThenBy(t => t.Id)
            };
    }
}
