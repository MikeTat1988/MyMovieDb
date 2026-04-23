using System.Text.Json;
using LocalMovieVault.Web.Data;
using LocalMovieVault.Web.Helpers;
using LocalMovieVault.Web.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace LocalMovieVault.Web.Services.Recommendations;

public sealed class DeterministicRecommendationEngine : IRecommendationEngine
{
    private readonly AppDbContext _dbContext;
    private readonly IRecommendationFeatureExtractor _featureExtractor;
    private readonly IRecommendationExplainer _explainer;
    private readonly AppUserPreferencesService _preferencesService;
    private readonly ILogger<DeterministicRecommendationEngine> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    public DeterministicRecommendationEngine(
        AppDbContext dbContext,
        IRecommendationFeatureExtractor featureExtractor,
        IRecommendationExplainer explainer,
        AppUserPreferencesService preferencesService,
        ILogger<DeterministicRecommendationEngine>? logger = null)
    {
        _dbContext = dbContext;
        _featureExtractor = featureExtractor;
        _explainer = explainer;
        _preferencesService = preferencesService;
        _logger = logger ?? NullLogger<DeterministicRecommendationEngine>.Instance;
    }

    public async Task RecalculateAsync(CancellationToken cancellationToken = default)
    {
        var movies = await _dbContext.Movies.ToListAsync(cancellationToken);
        if (movies.Count == 0)
        {
            return;
        }

        var preferences = _preferencesService.Get();
        var results = CalculateResults(movies, preferences);

        foreach (var movie in movies)
        {
            var result = results[movie.Id];
            movie.PersonalMatchScore = result.PersonalMatchScore;
            movie.PredictedScore = result.FinalScore;
            movie.PredictedLabel = result.PredictedLabel;
            movie.PredictedReason = result.PredictedReason;
            movie.RecommendationContextJson = JsonSerializer.Serialize(result.Context, JsonOptions);
            movie.PlotKeywordsCsv = RecommendationCatalog.JoinCsv(result.Context.PlotKeywords);

            _logger.LogDebug(
                "Recommendation for {MovieId} {Title}: affinity={AffinityScore:0.0} confidence={ConfidenceScore:0.0} predicted={PredictedScore:0.0} positives={PositiveSummary} negatives={NegativeSummary}",
                movie.Id,
                movie.Title,
                result.PersonalMatchScore,
                result.Context.ConfidenceScore,
                result.FinalScore,
                string.Join(", ", result.Context.PositiveFactors.OrderByDescending(x => x.Weight).Take(3).Select(x => x.Label)),
                string.Join(", ", result.Context.NegativeFactors.OrderByDescending(x => x.Weight).Take(2).Select(x => x.Label)));
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Recalculated recommendations for {MovieCount} movies.", movies.Count);
    }

    public IReadOnlyDictionary<int, RecommendationResult> CalculateResults(IReadOnlyList<Movie> movies, AppUserPreferences? overridePreferences = null)
    {
        if (movies.Count == 0)
        {
            return new Dictionary<int, RecommendationResult>();
        }

        foreach (var movie in movies)
        {
            EnsureCompatibilityFields(movie);
        }

        var preferences = overridePreferences ?? _preferencesService.Get();
        preferences.Normalize();
        var features = movies.ToDictionary(x => x.Id, _featureExtractor.Extract);
        var tasteProfile = BuildTasteProfile(movies, features, preferences);
        var results = new Dictionary<int, RecommendationResult>();

        foreach (var movie in movies)
        {
            var scoreProfile = BuildScoreProfileForMovie(movie, tasteProfile, features[movie.Id], preferences);
            var result = ScoreMovie(movie, scoreProfile, movies, features, preferences);
            results[movie.Id] = result;
        }

        ApplyCalibratedScores(movies, results);

        foreach (var movie in movies)
        {
            ApplyTastePriorityAdjustment(results[movie.Id], movie, features[movie.Id], preferences);
        }

        foreach (var movie in movies)
        {
            var result = results[movie.Id];
            result.Context.PlotKeywords = features[movie.Id].PlotKeywords.ToList();
            var predictedGrade = RecommendationViewHelper.MapScoreToGrade(result.FinalScore) ?? UserGrade.Meh;
            result.PredictedLabel = RecommendationViewHelper.GetGradeLabel(predictedGrade);
            result.Context.PredictedLabel = result.PredictedLabel;
            result.Context.FinalScore = result.FinalScore;
            result.PredictedReason = _explainer.BuildReason(result.Context);
        }

        return results;
    }

    private static void EnsureCompatibilityFields(Movie movie)
    {
        if (movie.UserRating is > 0m and <= 10m)
        {
            movie.UserRating *= 10m;
        }

        movie.UserGrade ??= RecommendationViewHelper.MapScoreToGrade(movie.UserRating)
            ?? (movie.PrimaryVerdict.HasValue ? RecommendationViewHelper.MapVerdictToGrade(movie.PrimaryVerdict.Value) : null);
        movie.PrimaryVerdict ??= RecommendationViewHelper.MapGradeToVerdict(movie.UserGrade);

        if (movie.UserGrade.HasValue && !movie.UserRating.HasValue)
        {
            movie.UserRating = RecommendationViewHelper.MapGradeToUserRating(movie.UserGrade.Value);
        }

        var migrated = RecommendationViewHelper.MigrateLegacyTags(movie.NormalizedTagsCsv ?? movie.ReasonTagsCsv);
        if (migrated.Count > 0)
        {
            movie.NormalizedTagsCsv = RecommendationViewHelper.JoinCsv(migrated);
            movie.ReasonTagsCsv = movie.NormalizedTagsCsv;
        }

        if (movie.UserGrade.HasValue && movie.WatchedStatus != WatchedStatus.Watched)
        {
            movie.WatchedStatus = WatchedStatus.Watched;
        }

        if (movie.WatchedStatus == WatchedStatus.Watched && movie.UserGrade.HasValue)
        {
            movie.NeedsTagReview = movie.NeedsTagReview
                || RecommendationViewHelper.SplitCsv(movie.NormalizedTagsCsv).Count() < RecommendationViewHelper.GetMinimumReasonTags(movie.UserGrade);
        }
    }

    private UserTasteProfile BuildTasteProfile(IReadOnlyList<Movie> movies, IReadOnlyDictionary<int, RecommendationFeatureSet> features, AppUserPreferences preferences)
    {
        var profile = new UserTasteProfile();
        var tuning = preferences.TasteTuning;

        foreach (var movie in movies)
        {
            if (!IsCompletedTasteAnchor(movie))
            {
                continue;
            }

            profile.RatedMovies.Add(movie);
            var weight = RecommendationCatalog.GetGradeWeight(movie.UserGrade.Value);
            var movieFeatures = features[movie.Id];

            AddWeights(profile.GenreWeights, movieFeatures.Genres, weight * 2m * tuning.GenreAffinityWeight);
            AddWeights(profile.GenrePairWeights, movieFeatures.GenrePairs, weight * 2.2m);
            AddWeights(profile.DirectorWeights, movieFeatures.Directors, weight * 1.7m * tuning.CreatorAffinityWeight);
            AddWeights(profile.WriterWeights, movieFeatures.Writers, weight * 1.4m * tuning.CreatorAffinityWeight);
            AddWeights(profile.ActorWeights, movieFeatures.Actors, weight * 1.2m);
            AddWeights(profile.CountryWeights, movieFeatures.Countries, weight * 0.6m);
            AddWeights(profile.LanguageWeights, movieFeatures.Languages, weight * 0.5m);
            AddWeights(profile.PlotKeywordWeights, movieFeatures.PlotKeywords, weight * 1.2m);
            AddReasonTagWeights(profile.ReasonTagWeights, RecommendationCatalog.SplitCsv(movie.NormalizedTagsCsv ?? movie.ReasonTagsCsv), weight, tuning);

            if (!string.IsNullOrWhiteSpace(movieFeatures.Decade))
            {
                AddWeight(profile.DecadeWeights, movieFeatures.Decade, weight * 0.4m);
            }

            if (!string.IsNullOrWhiteSpace(movieFeatures.RuntimeBucket))
            {
                AddWeight(profile.RuntimeWeights, movieFeatures.RuntimeBucket, weight * 0.4m);
            }
        }

        return profile;
    }

    private static UserTasteProfile BuildScoreProfileForMovie(
        Movie movie,
        UserTasteProfile baseProfile,
        RecommendationFeatureSet movieFeatures,
        AppUserPreferences preferences)
    {
        if (!IsCompletedTasteAnchor(movie))
        {
            return baseProfile;
        }

        var profile = CloneProfile(baseProfile);
        var tuning = preferences.TasteTuning;
        var weight = RecommendationCatalog.GetGradeWeight(movie.UserGrade.Value);

        profile.RatedMovies.RemoveAll(x => x.Id == movie.Id);
        AddWeights(profile.GenreWeights, movieFeatures.Genres, -weight * 2m * tuning.GenreAffinityWeight);
        AddWeights(profile.GenrePairWeights, movieFeatures.GenrePairs, -weight * 2.2m);
        AddWeights(profile.DirectorWeights, movieFeatures.Directors, -weight * 1.7m * tuning.CreatorAffinityWeight);
        AddWeights(profile.WriterWeights, movieFeatures.Writers, -weight * 1.4m * tuning.CreatorAffinityWeight);
        AddWeights(profile.ActorWeights, movieFeatures.Actors, -weight * 1.2m);
        AddWeights(profile.CountryWeights, movieFeatures.Countries, -weight * 0.6m);
        AddWeights(profile.LanguageWeights, movieFeatures.Languages, -weight * 0.5m);
        AddWeights(profile.PlotKeywordWeights, movieFeatures.PlotKeywords, -weight * 1.2m);
        AddReasonTagWeights(profile.ReasonTagWeights, RecommendationCatalog.SplitCsv(movie.NormalizedTagsCsv ?? movie.ReasonTagsCsv), -weight, tuning);

        if (!string.IsNullOrWhiteSpace(movieFeatures.Decade))
        {
            AddWeight(profile.DecadeWeights, movieFeatures.Decade, -weight * 0.4m);
        }

        if (!string.IsNullOrWhiteSpace(movieFeatures.RuntimeBucket))
        {
            AddWeight(profile.RuntimeWeights, movieFeatures.RuntimeBucket, -weight * 0.4m);
        }

        return profile;
    }

    private RecommendationResult ScoreMovie(
        Movie movie,
        UserTasteProfile profile,
        IReadOnlyList<Movie> allMovies,
        IReadOnlyDictionary<int, RecommendationFeatureSet> features,
        AppUserPreferences preferences)
    {
        var featureSet = features[movie.Id];
        var context = new RecommendationContext();

        var broadFit = 0m;
        broadFit += ApplyFeatureWeights(context.PositiveFactors, context.NegativeFactors, "genre", featureSet.Genres, profile.GenreWeights, 1.3m * preferences.GenrePreferenceWeight * preferences.TasteTuning.GenreAffinityWeight);
        broadFit += ApplyFeatureWeights(context.PositiveFactors, context.NegativeFactors, "genre mix", featureSet.GenrePairs, profile.GenrePairWeights, 1.0m);
        broadFit += ApplyFeatureWeights(context.PositiveFactors, context.NegativeFactors, "director", featureSet.Directors, profile.DirectorWeights, 1.1m * preferences.TasteTuning.CreatorAffinityWeight);
        broadFit += ApplyFeatureWeights(context.PositiveFactors, context.NegativeFactors, "writer", featureSet.Writers, profile.WriterWeights, 1.0m * preferences.TasteTuning.CreatorAffinityWeight);
        broadFit += ApplyFeatureWeights(context.PositiveFactors, context.NegativeFactors, "actor", featureSet.Actors, profile.ActorWeights, 0.6m);
        broadFit += ApplyFeatureWeights(context.PositiveFactors, context.NegativeFactors, "country", featureSet.Countries, profile.CountryWeights, 0.3m);
        broadFit += ApplyFeatureWeights(context.PositiveFactors, context.NegativeFactors, "language", featureSet.Languages, profile.LanguageWeights, 0.3m);
        broadFit += ApplyFeatureWeights(context.PositiveFactors, context.NegativeFactors, "story", featureSet.PlotKeywords, profile.PlotKeywordWeights, 0.8m * preferences.StoryPreferenceWeight);
        broadFit += ApplyReasonTagWeights(context.PositiveFactors, context.NegativeFactors, featureSet.ReasonTagHints, profile.ReasonTagWeights, preferences, featureSet.Genres, 1.4m * preferences.CinematographyPreferenceWeight * preferences.TasteTuning.PositiveExplicitTagWeight);
        broadFit += ApplyExplicitPreferenceAdjustments(context.PositiveFactors, context.NegativeFactors, "genre", featureSet.Genres, preferences.GenreAdjustments, 1.5m);
        broadFit += ApplyExplicitPreferenceAdjustments(context.PositiveFactors, context.NegativeFactors, "director", featureSet.Directors, preferences.DirectorAdjustments, 1.2m);
        broadFit += ApplyExplicitPreferenceAdjustments(context.PositiveFactors, context.NegativeFactors, "country", featureSet.Countries, preferences.CountryAdjustments, 0.55m);
        broadFit += ApplyExplicitPreferenceAdjustments(context.PositiveFactors, context.NegativeFactors, "language", featureSet.Languages, preferences.LanguageAdjustments, 0.55m);

        if (!string.IsNullOrWhiteSpace(featureSet.Decade))
        {
            broadFit += ApplyFeatureWeight(context.PositiveFactors, context.NegativeFactors, "decade", featureSet.Decade, profile.DecadeWeights, 0.4m);
        }

        if (!string.IsNullOrWhiteSpace(featureSet.RuntimeBucket))
        {
            broadFit += ApplyFeatureWeight(context.PositiveFactors, context.NegativeFactors, "runtime", featureSet.RuntimeBucket, profile.RuntimeWeights, 0.4m);
        }

        var similarity = ScoreSimilarity(movie, featureSet, profile, allMovies, features, context);
        var affinityBase = Math.Clamp(24m + (broadFit * 3.1m) + (similarity * 28m), 0m, 100m);
        var positiveTagScore = context.PositiveFactors
            .Where(x => x.Label.StartsWith("tag:", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.Weight)
            .Take(3)
            .Sum(x => x.Weight * 2.6m);
        var negativeTagScore = context.NegativeFactors
            .Where(x => x.Label.StartsWith("tag:", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.Weight)
            .Take(2)
            .Sum(x => x.Weight * 2.8m);
        positiveTagScore = Math.Clamp(positiveTagScore, 0m, 22m);
        negativeTagScore = Math.Clamp(negativeTagScore, 0m, 18m);

        var affinityScore = Math.Clamp(
            affinityBase
            + (positiveTagScore * 0.42m)
            - (negativeTagScore * 0.56m),
            0m,
            100m);
        var confidenceScore = CalculateConfidenceScore(featureSet, context, similarity, preferences);
        context.AffinityScore = decimal.Round(affinityScore, 1);
        context.ConfidenceScore = decimal.Round(confidenceScore, 1);
        context.FinalScore = decimal.Round(affinityScore, 1);

        if (confidenceScore < 45m)
        {
            context.WarningFactors.Add("Limited evidence");
        }
        else if (confidenceScore >= 72m)
        {
            context.WarningFactors.Add("Strong evidence");
        }

        var result = new RecommendationResult
        {
            FinalScore = decimal.Round(affinityScore, 1),
            PersonalMatchScore = decimal.Round(affinityScore, 1),
            Context = context
        };

        return result;
    }

    private decimal ScoreSimilarity(
        Movie movie,
        RecommendationFeatureSet candidate,
        UserTasteProfile profile,
        IReadOnlyList<Movie> allMovies,
        IReadOnlyDictionary<int, RecommendationFeatureSet> features,
        RecommendationContext context)
    {
        if (profile.RatedMovies.Count == 0)
        {
            return 0m;
        }

        var positive = new List<(Movie Movie, decimal Score)>();
        var negative = new List<(Movie Movie, decimal Score)>();

        foreach (var ratedMovie in profile.RatedMovies)
        {
            if (ratedMovie.Id == movie.Id || !ratedMovie.UserGrade.HasValue)
            {
                continue;
            }

            var similarity = ComputeSimilarity(candidate, features[ratedMovie.Id]);
            if (similarity <= 0)
            {
                continue;
            }

            if (RecommendationCatalog.GetGradeWeight(ratedMovie.UserGrade.Value) > 0)
            {
                positive.Add((ratedMovie, similarity));
            }
            else
            {
                negative.Add((ratedMovie, similarity));
            }
        }

        foreach (var item in positive.OrderByDescending(x => x.Score).Take(2))
        {
            context.SimilarToLiked.Add(new SimilarMovieSummary
            {
                Title = item.Movie.Title,
                Verdict = item.Movie.PrimaryVerdict ?? PersonalVerdict.Liked,
                SimilarityScore = Math.Round(item.Score * 100m, 1)
            });
        }

        foreach (var item in negative.OrderByDescending(x => x.Score).Take(2))
        {
            context.SimilarToDisliked.Add(new SimilarMovieSummary
            {
                Title = item.Movie.Title,
                Verdict = item.Movie.PrimaryVerdict ?? PersonalVerdict.AvoidType,
                SimilarityScore = Math.Round(item.Score * 100m, 1)
            });
        }

        var likedBoost = positive.OrderByDescending(x => x.Score).Take(3).Sum(x => x.Score);
        var dislikedPenalty = negative.OrderByDescending(x => x.Score).Take(2).Sum(x => x.Score);
        return likedBoost - dislikedPenalty;
    }

    private static bool IsCompletedTasteAnchor(Movie movie)
        => movie.WatchedStatus == WatchedStatus.Watched
            && movie.UserGrade.HasValue
            && !movie.NeedsTagReview;

    private static decimal ComputeSimilarity(RecommendationFeatureSet left, RecommendationFeatureSet right)
    {
        var total = 0m;
        total += IntersectScore(left.Genres, right.Genres, 0.18m);
        total += IntersectScore(left.GenrePairs, right.GenrePairs, 0.12m);
        total += IntersectScore(left.Directors, right.Directors, 0.14m);
        total += IntersectScore(left.Writers, right.Writers, 0.10m);
        total += IntersectScore(left.Actors, right.Actors, 0.08m);
        total += IntersectScore(left.PlotKeywords, right.PlotKeywords, 0.18m);
        total += IntersectScore(left.ReasonTagHints, right.ReasonTagHints, 0.20m);
        return total;
    }

    private static decimal IntersectScore(IReadOnlyList<string> left, IReadOnlyList<string> right, decimal weight)
    {
        if (left.Count == 0 || right.Count == 0)
        {
            return 0m;
        }

        var intersection = left.Intersect(right, StringComparer.OrdinalIgnoreCase).Count();
        if (intersection == 0)
        {
            return 0m;
        }

        return (intersection / (decimal)Math.Max(left.Count, right.Count)) * weight;
    }

    private static decimal ApplyFeatureWeights(List<ExplanationFactor> positives, List<ExplanationFactor> negatives, string labelPrefix, IEnumerable<string> values, IReadOnlyDictionary<string, decimal> weights, decimal multiplier)
    {
        var total = 0m;
        foreach (var value in values.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            total += ApplyFeatureWeight(positives, negatives, labelPrefix, value, weights, multiplier);
        }

        return total;
    }

    private static decimal ApplyExplicitPreferenceAdjustments(
        List<ExplanationFactor> positives,
        List<ExplanationFactor> negatives,
        string kind,
        IEnumerable<string> values,
        IReadOnlyDictionary<string, decimal> adjustments,
        decimal multiplier)
    {
        var total = 0m;
        foreach (var value in values.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!adjustments.TryGetValue(value, out var rawAdjustment) || rawAdjustment == 0m)
            {
                continue;
            }

            var weighted = rawAdjustment * multiplier;
            var direction = weighted > 0m ? "more" : "less";
            var factor = new ExplanationFactor($"preference: {direction} {kind} {value}", Math.Abs(weighted), weighted > 0m);
            if (weighted > 0m)
            {
                positives.Add(factor);
            }
            else
            {
                negatives.Add(factor);
            }

            total += weighted;
        }

        return total;
    }

    private static decimal ApplyReasonTagWeights(
        List<ExplanationFactor> positives,
        List<ExplanationFactor> negatives,
        IEnumerable<string> values,
        IReadOnlyDictionary<string, decimal> weights,
        AppUserPreferences preferences,
        IReadOnlyList<string> genres,
        decimal multiplier)
    {
        var total = 0m;
        foreach (var value in values.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var canonical = RecommendationViewHelper.CanonicalizeReasonTag(value);
            var normalized = RecommendationCatalog.NormalizeFeature(canonical);
            if (!weights.TryGetValue(normalized, out var rawWeight) || rawWeight == 0m)
            {
                continue;
            }

            var weighted = rawWeight * multiplier * GetReasonTagPreferenceMultiplier(canonical, preferences) * GetGenreMultiplier(canonical, genres);
            var factor = new ExplanationFactor($"tag: {canonical}", Math.Abs(weighted), weighted > 0);
            if (weighted > 0)
            {
                positives.Add(factor);
            }
            else
            {
                negatives.Add(factor);
            }

            total += weighted;
        }

        var canonicalValues = values
            .Select(RecommendationViewHelper.CanonicalizeReasonTag)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (canonicalValues.Contains("Great dialogue") && canonicalValues.Contains("Great acting"))
        {
            var synergy = 0.8m * preferences.TasteTuning.DialogueActingSynergyMultiplier;
            positives.Add(new ExplanationFactor("tag combo: Great dialogue + Great acting", synergy, true));
            total += synergy;
        }

        return total;
    }

    private static decimal GetGenreMultiplier(string tag, IReadOnlyList<string> genres)
    {
        var definition = RecommendationViewHelper.ReasonTagDefinitions.FirstOrDefault(x => string.Equals(x.Label, tag, StringComparison.OrdinalIgnoreCase));
        if (definition?.Genres is null || definition.Genres.Count == 0)
        {
            return 1.0m;
        }

        return genres.Any(x => definition.Genres.Contains(x, StringComparer.OrdinalIgnoreCase)) ? 1.35m : 0.85m;
    }

    private static decimal ApplyFeatureWeight(List<ExplanationFactor> positives, List<ExplanationFactor> negatives, string labelPrefix, string value, IReadOnlyDictionary<string, decimal> weights, decimal multiplier)
    {
        if (!weights.TryGetValue(value, out var rawWeight) || rawWeight == 0m)
        {
            return 0m;
        }

        var weighted = rawWeight * multiplier;
        var factor = new ExplanationFactor($"{labelPrefix}: {value}", Math.Abs(weighted), weighted > 0);
        if (weighted > 0)
        {
            positives.Add(factor);
        }
        else
        {
            negatives.Add(factor);
        }

        return weighted;
    }

    private static void AddWeights(IDictionary<string, decimal> target, IEnumerable<string> values, decimal weight)
    {
        foreach (var value in values)
        {
            AddWeight(target, value, weight);
        }
    }

    private static void AddReasonTagWeights(IDictionary<string, decimal> target, IEnumerable<string> values, decimal gradeWeight, TasteTuningSettings tuning)
    {
        foreach (var value in values)
        {
            var canonical = RecommendationViewHelper.CanonicalizeReasonTag(value);
            var normalized = RecommendationCatalog.NormalizeFeature(canonical);
            var baseWeight = RecommendationViewHelper.GetBaseReasonTagWeight(canonical) * GetReasonTagTuningMultiplier(canonical, tuning);
            AddWeight(target, normalized, gradeWeight * 1.6m * baseWeight);
        }
    }

    private static void AddWeight(IDictionary<string, decimal> target, string value, decimal weight)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (target.TryGetValue(value, out var current))
        {
            target[value] = current + weight;
            return;
        }

        target[value] = weight;
    }

    private static UserTasteProfile CloneProfile(UserTasteProfile source)
    {
        var clone = new UserTasteProfile();
        CopyWeights(source.GenreWeights, clone.GenreWeights);
        CopyWeights(source.GenrePairWeights, clone.GenrePairWeights);
        CopyWeights(source.DirectorWeights, clone.DirectorWeights);
        CopyWeights(source.WriterWeights, clone.WriterWeights);
        CopyWeights(source.ActorWeights, clone.ActorWeights);
        CopyWeights(source.CountryWeights, clone.CountryWeights);
        CopyWeights(source.LanguageWeights, clone.LanguageWeights);
        CopyWeights(source.DecadeWeights, clone.DecadeWeights);
        CopyWeights(source.RuntimeWeights, clone.RuntimeWeights);
        CopyWeights(source.PlotKeywordWeights, clone.PlotKeywordWeights);
        CopyWeights(source.ReasonTagWeights, clone.ReasonTagWeights);
        clone.RatedMovies.AddRange(source.RatedMovies);
        return clone;
    }

    private static void CopyWeights(
        IReadOnlyDictionary<string, decimal> source,
        IDictionary<string, decimal> target)
    {
        foreach (var entry in source)
        {
            target[entry.Key] = entry.Value;
        }
    }

    private static decimal GetReasonTagPreferenceMultiplier(string label, AppUserPreferences preferences)
    {
        var importantBoost = preferences.GetImportantTags().Contains(label, StringComparer.OrdinalIgnoreCase)
            ? preferences.TasteTuning.ImportantTagMultiplier
            : 1.0m;
        return importantBoost;
    }

    private static decimal CalculateConfidenceScore(
        RecommendationFeatureSet featureSet,
        RecommendationContext context,
        decimal similarity,
        AppUserPreferences preferences)
    {
        var strongPositiveCount = context.PositiveFactors.Count(x => x.Weight >= 0.8m);
        var strongNegativeCount = context.NegativeFactors.Count(x => x.Weight >= 0.8m);
        var similarityEvidence = Math.Clamp(Math.Abs(similarity) * 100m, 0m, 22m);

        return Math.Clamp(
            18m
            + (featureSet.MetadataQualityScore * 0.34m)
            + (featureSet.QualityConfidence * 0.22m * preferences.ImdbPreferenceWeight)
            + (strongPositiveCount * 4.5m)
            + similarityEvidence
            - (strongNegativeCount * 1.5m),
            0m,
            100m);
    }

    private static void ApplyCalibratedScores(
        IReadOnlyList<Movie> movies,
        IDictionary<int, RecommendationResult> results)
    {
        var ordered = movies
            .Select(movie => (Movie: movie, Result: results[movie.Id]))
            .OrderByDescending(x => x.Result.PersonalMatchScore)
            .ThenByDescending(x => x.Result.Context.ConfidenceScore)
            .ThenBy(x => x.Movie.Title, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Movie.Id)
            .ToList();

        for (var index = 0; index < ordered.Count; index++)
        {
            var entry = ordered[index];
            var percentile = ordered.Count == 1
                ? 1m
                : 1m - (index / (decimal)(ordered.Count - 1));
            var rankBonus = percentile * 2.5m;
            var confidenceAdjustment = (entry.Result.Context.ConfidenceScore - 58m) * 0.08m;
            var calibratedScore = Math.Clamp(
                (entry.Result.PersonalMatchScore * 0.87m)
                + rankBonus
                + confidenceAdjustment,
                0m,
                94m);

            entry.Result.FinalScore = decimal.Round(calibratedScore, 1);
            entry.Result.Context.FinalScore = entry.Result.FinalScore;
        }
    }

    private static void ApplyTastePriorityAdjustment(
        RecommendationResult result,
        Movie movie,
        RecommendationFeatureSet featureSet,
        AppUserPreferences preferences)
    {
        if (preferences.GetImportantTags().Count == 0 || movie.WatchedStatus == WatchedStatus.Watched)
        {
            return;
        }

        var source = string.Join(
            ' ',
            RecommendationViewHelper.SplitCsv(movie.NormalizedTagsCsv ?? movie.ReasonTagsCsv)
                .Concat(featureSet.ReasonTagHints.Select(RecommendationViewHelper.CanonicalizeReasonTag))
                .Concat(featureSet.PlotKeywords))
            .ToLowerInvariant();
        var totalBonus = 0m;

        foreach (var tag in preferences.GetImportantTags())
        {
            if (!MatchesImportantTagSignal(tag, source))
            {
                continue;
            }

            var bonus = 2.4m * (preferences.TasteTuning.ImportantTagMultiplier - 1m) * GetGenreMultiplier(tag, featureSet.Genres);
            result.Context.PositiveFactors.Add(new ExplanationFactor($"priority: {tag}", bonus, true));
            totalBonus += bonus;
        }

        if (totalBonus <= 0m)
        {
            return;
        }

        result.FinalScore = decimal.Round(Math.Clamp(result.FinalScore + Math.Min(totalBonus, 12m), 0m, 100m), 1);
        result.Context.FinalScore = result.FinalScore;
    }

    private static bool MatchesImportantTagSignal(string tag, string source)
    {
        if (source.Contains(tag, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!RecommendationCatalog.ReasonTagHintMap.TryGetValue(tag, out var hints))
        {
            return false;
        }

        return hints.Any(hint => source.Contains(hint, StringComparison.OrdinalIgnoreCase));
    }

    private static decimal GetReasonTagTuningMultiplier(string label, TasteTuningSettings tuning)
    {
        var canonical = RecommendationViewHelper.CanonicalizeReasonTag(label);
        return canonical switch
        {
            "Original idea" or "Incredible visuals" => tuning.CrossGenreAnchorWeight,
            "Too slow" or "Too long" => tuning.NegativePacingPenaltyWeight,
            _ => 1.0m
        };
    }
}
