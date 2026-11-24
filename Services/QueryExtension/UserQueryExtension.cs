using Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace Services.QueryExtension
{
    internal static class UserQueryExtension
    {
        private const string LikeEscapeCharacter = "\\";

        internal static IQueryable<User> ApplySearch(this IQueryable<User> query, string? search)
        {
            if (!string.IsNullOrWhiteSpace(search))
            {
                var terms = search.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                foreach (var term in terms)
                {
                    var pattern = $"%{EscapeLike(term)}%";

                    query = query.Where(u =>
                        EF.Functions.ILike(EF.Functions.Unaccent(u.FirstName), EF.Functions.Unaccent(pattern), LikeEscapeCharacter) ||
                        EF.Functions.ILike(EF.Functions.Unaccent(u.LastName), EF.Functions.Unaccent(pattern), LikeEscapeCharacter));
                }
            }

            return query;
        }

        internal static IQueryable<User> ApplySorting(
            this IQueryable<User> query,
            string? sortBy,
            bool sortDescending) => sortBy?.Trim().ToLowerInvariant() switch
            {
                "createdat" => sortDescending
                    ? query.OrderByDescending(u => u.CreatedAt).ThenByDescending(u => u.Id)
                    : query.OrderBy(u => u.CreatedAt).ThenBy(u => u.Id),

                "lastname" => sortDescending
                    ? query.OrderByDescending(u => u.LastName).ThenByDescending(u => u.FirstName).ThenByDescending(u => u.Id)
                    : query.OrderBy(u => u.LastName).ThenBy(u => u.FirstName).ThenBy(u => u.Id),

                "firstname" => sortDescending
                    ? query.OrderByDescending(u => u.FirstName).ThenByDescending(u => u.LastName).ThenByDescending(u => u.Id)
                    : query.OrderBy(u => u.FirstName).ThenBy(u => u.LastName).ThenBy(u => u.Id),

                _ => query
                    .OrderBy(u => u.FirstName)
                    .ThenBy(u => u.LastName)
                    .ThenBy(u => u.Id)
            };

        private static string EscapeLike(string term)
            => term
                .Replace("\\", "\\\\")
                .Replace("%", "\\%")
                .Replace("_", "\\_");
    }
}
