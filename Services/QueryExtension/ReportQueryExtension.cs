using Domain.Enum;
using Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace Services.QueryExtension
{
    internal static class ReportQueryExtension
    {
        internal static IQueryable<UserReport> ApplySearch(this IQueryable<UserReport> query, string? search)
        {
            if (!string.IsNullOrWhiteSpace(search))
            {
                var terms = search.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                foreach (var term in terms)
                {
                    var pattern = $"%{term}%";

                    query = query.Where(t =>
                        EF.Functions.ILike(EF.Functions.Unaccent(t.ReportedUser.FirstName), EF.Functions.Unaccent(pattern)) ||
                        EF.Functions.ILike(EF.Functions.Unaccent(t.ReportedUser.LastName), EF.Functions.Unaccent(pattern)) ||
                        (t.Reason != null && EF.Functions.ILike(EF.Functions.Unaccent(t.Reason), EF.Functions.Unaccent(pattern))));
                }
            }

            return query;
        }

        public static IQueryable<UserReport> ApplySorting(
            this IQueryable<UserReport> query,
            string? sortBy,
            bool sortDescending) => sortBy?.ToLowerInvariant() switch
            {
                "createdat" => sortDescending
                    ? query.OrderByDescending(r => r.CreatedAt)
                    : query.OrderBy(r => r.CreatedAt),

                "resolvedat" => sortDescending
                    ? query.OrderByDescending(r => r.ResolvedAt)
                    : query.OrderBy(r => r.ResolvedAt),

                "status" => sortDescending
                        ? query.OrderByDescending(r => r.Status)
                        : query.OrderBy(r => r.Status),

                _ => query.OrderByDescending(r => r.CreatedAt)
            };

        public static IQueryable<UserReport> ApplyFilter(
            this IQueryable<UserReport> query,
            bool isShowAll = false
            )
        {
            if (isShowAll == true) return query;


            return query.Where(r => r.Status == ReportStatusEnum.Pending);
        }
    }
}
