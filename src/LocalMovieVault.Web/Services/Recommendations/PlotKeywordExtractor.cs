using System.Text.RegularExpressions;
using LocalMovieVault.Web.Models;

namespace LocalMovieVault.Web.Services.Recommendations;

public sealed class PlotKeywordExtractor : IPlotKeywordExtractor
{
    public IReadOnlyList<string> ExtractKeywords(Movie movie)
    {
        var source = movie.Overview;
        if (string.IsNullOrWhiteSpace(source))
        {
            return [];
        }

        var tokens = Regex.Matches(source.ToLowerInvariant(), @"[a-z0-9][a-z0-9'\-]{2,}")
            .Select(x => x.Value.Trim('\'', '-'))
            .Where(IsMeaningfulKeyword)
            .GroupBy(x => x)
            .OrderByDescending(x => x.Count())
            .ThenBy(x => x.Key, StringComparer.Ordinal)
            .Take(12)
            .Select(x => x.Key)
            .ToList();

        return tokens;
    }

    private static bool IsMeaningfulKeyword(string token)
    {
        if (token.Length < 4)
        {
            return false;
        }

        if (!token.Any(char.IsLetter))
        {
            return false;
        }

        if (token.Contains('\'', StringComparison.Ordinal) || token.Contains('-', StringComparison.Ordinal))
        {
            return false;
        }

        return !RecommendationCatalog.KeywordStopWords.Contains(token)
            && !RecommendationCatalog.GenericPlotKeywordWords.Contains(token);
    }
}
