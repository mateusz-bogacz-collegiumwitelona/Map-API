using Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace Services.QueryExtension
{
    internal static class PlaceQueryExtension
    {
        internal static IQueryable<Place> ApplySearch(
            this IQueryable<Place> query,
            string search)
        {
            var searchPattern = $"{search.Trim()}%";

            return query.Where(
                place => EF.Functions.ILike(place.Name, searchPattern));
        }
    }
}