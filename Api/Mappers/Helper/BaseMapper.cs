using PgbNameCase;
using Riok.Mapperly.Abstractions;

namespace Api.Mappers.Helper
{
    public abstract class BaseMapper
    {
        [UserMapping(Default = false)]
        protected static string? NormalizeNullableName(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;

            var trimmed = value.Trim();

            return NameCaseFormatter.Format(trimmed);
        }
    }
}