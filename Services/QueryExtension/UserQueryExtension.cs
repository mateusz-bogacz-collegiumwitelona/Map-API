using Domain.Models;
using Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Services.QueryExtension
{
    internal static class UserQueryExtension
    {
        internal static IQueryable<User> ApplySearch(
            this IQueryable<User> query,
            string? search,
            AppDbContext context)
        {
            if (!string.IsNullOrWhiteSpace(search))
            {
                var terms = search.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                foreach (var term in terms)
                {
                    var pattern = $"%{term}%";

                    query = query.Where(u =>
                        (u.FirstName != null && EF.Functions.ILike(EF.Functions.Unaccent(u.FirstName), EF.Functions.Unaccent(pattern))) ||
                        (u.LastName != null && EF.Functions.ILike(EF.Functions.Unaccent(u.LastName), EF.Functions.Unaccent(pattern))) ||
                        (u.Email != null && EF.Functions.ILike(EF.Functions.Unaccent(u.Email), EF.Functions.Unaccent(pattern))) ||
                        context.UserRoles
                            .Where(ur => ur.UserId == u.Id)
                            .Join(context.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => r.Name)
                            .Any(roleName => roleName != null && EF.Functions.ILike(EF.Functions.Unaccent(roleName), EF.Functions.Unaccent(pattern)))
                    );
                }
            }

            return query;
        }

        internal static IQueryable<User> ApplyFilter(
            this IQueryable<User> query,
            bool includeBanned,
            bool includeAdmins,
            AppDbContext context
            )
        {
            if (!includeBanned)
            {
                query = query.Where(u => !u.IsBanned);
            }

            if (!includeAdmins)
            {
                query = query.Where(u => !context.UserRoles
                    .Where(ur => ur.UserId == u.Id)
                    .Join(context.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => r.Name)
                    .Any(roleName => roleName == "Admin"));
            }

            return query;
        }

        public static IQueryable<User> ApplySorting(
            this IQueryable<User> query,
            string? sortBy,
            bool sortDescending) => sortBy?.ToLowerInvariant() switch
            {
                "firstname" => sortDescending
                    ? query.OrderByDescending(u => u.FirstName).ThenByDescending(u => u.Id)
                    : query.OrderBy(u => u.FirstName).ThenBy(u => u.Id),

                "lastname" => sortDescending
                    ? query.OrderByDescending(u => u.LastName).ThenByDescending(u => u.Id)
                    : query.OrderBy(u => u.LastName).ThenBy(u => u.Id),

                "email" => sortDescending
                    ? query.OrderByDescending(u => u.Email).ThenByDescending(u => u.Id)
                    : query.OrderBy(u => u.Email).ThenBy(u => u.Id),

                _ => sortDescending
                    ? query.OrderByDescending(u => u.CreatedAt).ThenByDescending(u => u.Id)
                    : query.OrderBy(u => u.CreatedAt).ThenBy(u => u.Id)
            };
    }
}
