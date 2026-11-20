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
                        EF.Functions.ILike(u.FirstName, pattern, LikeEscapeCharacter) ||
                        EF.Functions.ILike(u.LastName, pattern, LikeEscapeCharacter));
                }
            }

            return query;
        }

        internal static IQueryable<User> ApplySorting(IQueryable<User> query, string? sortBy) =>
            sortBy?.Trim().ToLowerInvariant() switch
            {
                "name_desc" => query
                    .OrderByDescending(u => u.FirstName)
                    .ThenByDescending(u => u.LastName)
                    .ThenByDescending(u => u.Id),

                "newest" => query
                    .OrderByDescending(u => u.CreatedAt)
                    .ThenByDescending(u => u.Id),

                "oldest" => query
                    .OrderBy(u => u.CreatedAt)
                    .ThenBy(u => u.Id),

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
