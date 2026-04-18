using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace LocalMovieVault.Web.Services;

public static class TitleNormalizer
{
    public static string Normalize(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return string.Empty;
        }

        var decomposed = title.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();

        foreach (var character in decomposed)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
            }
        }

        return Regex.Replace(builder.ToString(), @"\s+", string.Empty);
    }
}
