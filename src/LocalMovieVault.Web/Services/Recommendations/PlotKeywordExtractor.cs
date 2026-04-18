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
            .Where(x => x.Length >= 3)
            .Where(x => !RecommendationCatalog.KeywordStopWords.Contains(x))
            .GroupBy(x => x)
            .OrderByDescending(x => x.Count())
            .ThenBy(x => x.Key, StringComparer.Ordinal)
            .Take(12)
            .Select(x => x.Key)
            .ToList();

        return tokens;
    }
}
