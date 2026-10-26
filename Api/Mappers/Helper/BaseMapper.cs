using Riok.Mapperly.Abstractions;
using System.Globalization;

namespace Api.Mappers.Helper
{
    public abstract class BaseMapper
    {
        private static readonly TextInfo _textInfo = CultureInfo.InvariantCulture.TextInfo;

        [UserMapping(Default = false)]
        protected static string? NormalizeNullableName(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;

            var trimmed = value.Trim();
            return _textInfo.ToTitleCase(trimmed.ToLowerInvariant());
        }
    }
}
