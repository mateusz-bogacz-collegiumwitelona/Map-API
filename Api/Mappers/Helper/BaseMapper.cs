using Riok.Mapperly.Abstractions;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Api.Mappers.Helper
{
    public abstract class BaseMapper
    {
        private static readonly TextInfo _textInfo = CultureInfo.InvariantCulture.TextInfo;

        private static readonly Regex NameExceptionsRegex = new(
            @"\b(Mc|O'|D'|L')([a-z])",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        [UserMapping(Default = false)]
        protected static string? NormalizeNullableName(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;

            var trimmed = value.Trim();

            var titleCased = _textInfo.ToTitleCase(trimmed.ToLowerInvariant());

            return NameExceptionsRegex.Replace(titleCased, m =>
                m.Groups[1].Value + m.Groups[2].Value.ToUpperInvariant());
        }
    }
}