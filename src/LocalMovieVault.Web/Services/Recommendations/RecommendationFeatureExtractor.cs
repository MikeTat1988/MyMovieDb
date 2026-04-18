using LocalMovieVault.Web.Models;

namespace LocalMovieVault.Web.Services.Recommendations;

public sealed class RecommendationFeatureExtractor : IRecommendationFeatureExtractor
{
    private readonly IPlotKeywordExtractor _plotKeywordExtractor;

    public RecommendationFeatureExtractor(IPlotKeywordExtractor plotKeywordExtractor)
    {
        _plotKeywordExtractor = plotKeywordExtractor;
    }

    public RecommendationFeatureSet Extract(Movie movie)
    {
        var genres = NormalizeList(movie.GenresCsv);
        var plotKeywords = RecommendationCatalog.SplitCsv(movie.TmdbKeywordsCsv);
        if (plotKeywords.Count == 0)
        {
            plotKeywords = RecommendationCatalog.SplitCsv(movie.PlotKeywordsCsv);
        }
        if (plotKeywords.Count == 0)
        {
            plotKeywords = _plotKeywordExtractor.ExtractKeywords(movie);
        }

        var explicitReasonTagHints = RecommendationCatalog.GetReasonTagHints(
            RecommendationCatalog.SplitCsv(movie.NormalizedTagsCsv ?? movie.ReasonTagsCsv),
            movie.Overview,
            plotKeywords);
        var inferredReasonTagHints = RecommendationCatalog.InferReasonTagHints(movie.Overview, plotKeywords);
        var reasonTagHints = explicitReasonTagHints
            .Concat(inferredReasonTagHints)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var qualityConfidence = CalculateQualityConfidence(movie);
        var metadataQualityScore = CalculateMetadataQualityScore(movie);

        return new RecommendationFeatureSet(
            Genres: genres,
            GenrePairs: RecommendationCatalog.BuildGenrePairs(genres),
            Directors: NormalizeList(movie.Director),
            Writers: NormalizeList(movie.Writer),
            Actors: NormalizeList(movie.Actors).Take(5).ToList(),
            Countries: NormalizeList(movie.Country),
            Languages: NormalizeList(movie.Language),
            PlotKeywords: plotKeywords.Select(RecommendationCatalog.NormalizeFeature).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            ReasonTagHints: reasonTagHints.Select(RecommendationCatalog.NormalizeFeature).ToList(),
            Decade: RecommendationCatalog.GetDecade(movie.Year),
            RuntimeBucket: RecommendationCatalog.GetRuntimeBucket(movie.RuntimeMinutes),
            QualityConfidence: qualityConfidence,
            MetadataQualityScore: metadataQualityScore);
    }

    private static List<string> NormalizeList(string? csv)
        => RecommendationCatalog.SplitCsv(csv)
            .Select(RecommendationCatalog.NormalizeFeature)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static decimal CalculateMetadataQualityScore(Movie movie)
    {
        var parts = new[]
        {
            !string.IsNullOrWhiteSpace(movie.GenresCsv),
            !string.IsNullOrWhiteSpace(movie.Director),
            !string.IsNullOrWhiteSpace(movie.Writer),
            !string.IsNullOrWhiteSpace(movie.Actors),
            movie.Year.HasValue,
            movie.RuntimeMinutes.HasValue,
            !string.IsNullOrWhiteSpace(movie.Country),
            !string.IsNullOrWhiteSpace(movie.Language),
            !string.IsNullOrWhiteSpace(movie.Overview),
            movie.ImdbRating.HasValue,
            movie.ImdbVotes.HasValue,
            movie.Metascore.HasValue
        };

        return decimal.Round(100m * parts.Count(x => x) / parts.Length, 1);
    }

    private static decimal CalculateQualityConfidence(Movie movie)
    {
        var imdbRatingPart = movie.ImdbRating.HasValue
            ? Math.Clamp((movie.ImdbRating.Value - 5m) * 10m, 0m, 50m)
            : 0m;

        var voteCount = movie.ImdbVotes ?? 0;
        var voteConfidence = voteCount switch
        {
            >= 500000 => 30m,
            >= 100000 => 24m,
            >= 25000 => 18m,
            >= 5000 => 12m,
            >= 1000 => 7m,
            > 0 => 3m,
            _ => 0m
        };

        var metascorePart = movie.Metascore.HasValue
            ? Math.Clamp((movie.Metascore.Value - 40) / 2m, 0m, 20m)
            : 0m;

        return decimal.Round(Math.Clamp(imdbRatingPart + voteConfidence + metascorePart, 0m, 100m), 1);
    }
}
