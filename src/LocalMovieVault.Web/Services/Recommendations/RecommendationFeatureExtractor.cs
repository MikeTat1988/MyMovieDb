using LocalMovieVault.Web.Models;
using LocalMovieVault.Web.Helpers;

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
        var hybridSignals = BuildHybridSignals(genres);
        var toneSignals = BuildToneSignals(genres, reasonTagHints, plotKeywords, movie.Overview);

        var qualityConfidence = CalculateQualityConfidence(movie);
        var metadataQualityScore = CalculateMetadataQualityScore(movie);

        return new RecommendationFeatureSet(
            Genres: genres,
            GenrePairs: RecommendationCatalog.BuildGenrePairs(genres),
            HybridSignals: hybridSignals,
            ToneSignals: toneSignals,
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

    private static IReadOnlyList<string> BuildHybridSignals(IReadOnlyList<string> genres)
    {
        var signals = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var genreSet = genres.ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (genres.Count > 1 && genreSet.Contains("comedy"))
        {
            signals.Add("comedy-hybrid");
        }

        if (genres.Count > 1 && genreSet.Contains("drama"))
        {
            signals.Add("drama-hybrid");
        }

        if (genres.Count > 1 && genreSet.Contains("horror"))
        {
            signals.Add("horror-hybrid");
        }

        if (genres.Count > 1 && genreSet.Contains("action"))
        {
            signals.Add("action-hybrid");
        }

        AddPairSignal(genreSet, signals, "sci-fi", "comedy");
        AddPairSignal(genreSet, signals, "sci-fi", "drama");
        AddPairSignal(genreSet, signals, "horror", "drama");
        AddPairSignal(genreSet, signals, "horror", "comedy");
        AddPairSignal(genreSet, signals, "action", "drama");
        AddPairSignal(genreSet, signals, "thriller", "drama");
        AddPairSignal(genreSet, signals, "fantasy", "drama");
        AddPairSignal(genreSet, signals, "mystery", "horror");

        return signals.Select(RecommendationCatalog.NormalizeFeature).ToList();
    }

    private static IReadOnlyList<string> BuildToneSignals(
        IReadOnlyList<string> genres,
        IReadOnlyList<string> reasonTagHints,
        IReadOnlyList<string> plotKeywords,
        string? overview)
    {
        var signals = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var genreSet = genres.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var tagSet = reasonTagHints
            .Select(RecommendationViewHelper.CanonicalizeReasonTag)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var source = ((overview ?? string.Empty) + " " + string.Join(' ', plotKeywords)).ToLowerInvariant();

        if (genreSet.Contains("comedy") || tagSet.Contains("Funny") || tagSet.Contains("Charming") || source.Contains("comedy", StringComparison.OrdinalIgnoreCase))
        {
            signals.Add("humorous");
            signals.Add("playful");
        }

        if (genreSet.Contains("drama") || tagSet.Contains("Emotional") || tagSet.Contains("Thought-provoking"))
        {
            signals.Add("emotional");
            signals.Add("reflective");
            signals.Add("serious");
        }

        if (genreSet.Contains("thriller") || genreSet.Contains("mystery") || tagSet.Contains("Tense"))
        {
            signals.Add("tense");
            signals.Add("serious");
        }

        if (genreSet.Contains("horror") || tagSet.Contains("Scary") || tagSet.Contains("Disturbing atmosphere"))
        {
            signals.Add("dread");
            signals.Add("dark");
            signals.Add("serious");
        }

        if (genreSet.Contains("action") || genreSet.Contains("adventure") || tagSet.Contains("Exciting action") || tagSet.Contains("Epic scale"))
        {
            signals.Add("spectacle");
            signals.Add("kinetic");
        }

        if (tagSet.Contains("Original idea") || tagSet.Contains("Weird in a good way") || source.Contains("surreal", StringComparison.OrdinalIgnoreCase) || source.Contains("strange", StringComparison.OrdinalIgnoreCase))
        {
            signals.Add("weird");
        }

        if (tagSet.Contains("Immersive") || tagSet.Contains("Disturbing atmosphere") || source.Contains("haunted", StringComparison.OrdinalIgnoreCase) || source.Contains("quiet", StringComparison.OrdinalIgnoreCase) || source.Contains("old house", StringComparison.OrdinalIgnoreCase))
        {
            signals.Add("atmospheric");
        }

        if ((genreSet.Contains("horror") || genreSet.Contains("mystery")) && !signals.Contains("spectacle") && !signals.Contains("humorous"))
        {
            signals.Add("atmospheric");
        }

        if (genreSet.Contains("horror") && IsQuietDreadSource(source, tagSet))
        {
            signals.Add("quiet-dread");
            signals.Add("atmospheric");
            signals.Add("reflective");
        }

        if (genreSet.Contains("horror") && IsIntenseHorrorSource(source, tagSet))
        {
            signals.Add("intense-horror");
            signals.Add("kinetic");
        }

        if (genreSet.Contains("sci-fi") && !signals.Contains("humorous"))
        {
            signals.Add("reflective");
        }

        return signals.Select(RecommendationCatalog.NormalizeFeature).ToList();
    }

    private static void AddPairSignal(IReadOnlySet<string> genres, ISet<string> signals, string left, string right)
    {
        if (genres.Contains(left) && genres.Contains(right))
        {
            signals.Add($"{left}+{right}");
        }
    }

    private static bool IsQuietDreadSource(string source, IReadOnlySet<string> tagSet)
        => tagSet.Contains("Disturbing atmosphere")
           || source.Contains("quiet", StringComparison.OrdinalIgnoreCase)
           || source.Contains("old house", StringComparison.OrdinalIgnoreCase)
           || source.Contains("elderly", StringComparison.OrdinalIgnoreCase)
           || source.Contains("nurse", StringComparison.OrdinalIgnoreCase)
           || source.Contains("author", StringComparison.OrdinalIgnoreCase)
           || source.Contains("ghost", StringComparison.OrdinalIgnoreCase)
           || source.Contains("haunted", StringComparison.OrdinalIgnoreCase)
           || source.Contains("whisper", StringComparison.OrdinalIgnoreCase)
           || source.Contains("manuscript", StringComparison.OrdinalIgnoreCase);

    private static bool IsIntenseHorrorSource(string source, IReadOnlySet<string> tagSet)
        => tagSet.Contains("Scary")
           || source.Contains("possession", StringComparison.OrdinalIgnoreCase)
           || source.Contains("demonic", StringComparison.OrdinalIgnoreCase)
           || source.Contains("summon", StringComparison.OrdinalIgnoreCase)
           || source.Contains("violent", StringComparison.OrdinalIgnoreCase)
           || source.Contains("bloody", StringComparison.OrdinalIgnoreCase)
           || source.Contains("terror", StringComparison.OrdinalIgnoreCase)
           || source.Contains("teens", StringComparison.OrdinalIgnoreCase)
           || source.Contains("friends", StringComparison.OrdinalIgnoreCase)
           || source.Contains("party", StringComparison.OrdinalIgnoreCase);

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
