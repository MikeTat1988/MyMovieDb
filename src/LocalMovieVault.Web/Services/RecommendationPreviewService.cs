using LocalMovieVault.Web.Data;
using LocalMovieVault.Web.Helpers;
using LocalMovieVault.Web.Models;
using LocalMovieVault.Web.Services.Recommendations;
using LocalMovieVault.Web.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace LocalMovieVault.Web.Services;

public sealed class RecommendationPreviewService
{
    private readonly AppDbContext _dbContext;
    private readonly AppUserPreferencesService _preferencesService;
    private readonly DeterministicRecommendationEngine _recommendationEngine;

    public RecommendationPreviewService(
        AppDbContext dbContext,
        AppUserPreferencesService preferencesService,
        IRecommendationEngine recommendationEngine)
    {
        _dbContext = dbContext;
        _preferencesService = preferencesService;
        _recommendationEngine = (DeterministicRecommendationEngine)recommendationEngine;
    }

    public async Task<AppSettingsViewModel> BuildViewModelAsync(
        AppUserPreferences previewPreferences,
        int? previewMovieId,
        bool randomize,
        CancellationToken cancellationToken)
    {
        var storedPreferences = _preferencesService.Get();
        var movies = await _dbContext.Movies.AsNoTracking().ToListAsync(cancellationToken);
        var currentResults = _recommendationEngine.CalculateResults(movies, storedPreferences);
        var previewResults = _recommendationEngine.CalculateResults(movies, previewPreferences);
        var previewMovie = SelectPreviewMovie(movies, currentResults, previewResults, previewMovieId, randomize);

        return BuildViewModel(storedPreferences, previewPreferences, movies, previewMovie, currentResults, previewResults);
    }

    private static AppSettingsViewModel BuildViewModel(
        AppUserPreferences storedPreferences,
        AppUserPreferences previewPreferences,
        List<Movie> movies,
        Movie? previewMovie,
        IReadOnlyDictionary<int, RecommendationResult> currentResults,
        IReadOnlyDictionary<int, RecommendationResult> previewResults)
    {
        var currentScore = previewMovie is not null && currentResults.TryGetValue(previewMovie.Id, out var current)
            ? current.FinalScore
            : 0m;
        var previewScore = previewMovie is not null && previewResults.TryGetValue(previewMovie.Id, out var projected)
            ? projected.FinalScore
            : currentScore;

        return new AppSettingsViewModel
        {
            Preferences = storedPreferences,
            Genres = GenreHelper.CollectGenres(movies),
            ImportantTagOptions = RecommendationViewHelper.ImportantTagOptions.ToList(),
            TastePrioritySelections = BuildTastePrioritySelections(previewPreferences),
            PreviewMovie = previewMovie,
            PreviewCurrentPredictedScore = decimal.Round(currentScore, 1),
            PreviewPredictedScore = decimal.Round(previewScore, 1),
            PreviewStatusText = previewMovie is null
                ? "No mismatch candidate is available for preview right now. Apply to recalculate the full library."
                : $"Current prediction: {decimal.Round(currentScore, 1):0.#} -> {decimal.Round(previewScore, 1):0.#}"
        };
    }

    private static List<string> BuildTastePrioritySelections(AppUserPreferences preferences)
    {
        var selections = preferences.GetImportantTags().ToList();
        while (selections.Count < 4)
        {
            selections.Add(string.Empty);
        }

        return selections;
    }

    private static Movie? SelectPreviewMovie(
        List<Movie> movies,
        IReadOnlyDictionary<int, RecommendationResult> currentResults,
        IReadOnlyDictionary<int, RecommendationResult> previewResults,
        int? previewMovieId,
        bool randomize)
    {
        if (previewMovieId.HasValue && !randomize)
        {
            var selectedMovie = movies.FirstOrDefault(x => x.Id == previewMovieId.Value);
            if (selectedMovie is not null && HasMeaningfulPreviewDelta(selectedMovie, currentResults, previewResults))
            {
                return selectedMovie;
            }
        }

        var rankedCandidates = movies
            .Where(x => x.WatchedStatus != WatchedStatus.Watched && !x.IsDismissed)
            .Select(x => new
            {
                Movie = x,
                CurrentScore = currentResults.TryGetValue(x.Id, out var current) ? current.FinalScore : x.PredictedScore ?? 0m,
                Delta = Math.Abs((previewResults.TryGetValue(x.Id, out var projected) ? projected.FinalScore : 0m) - (currentResults.TryGetValue(x.Id, out current) ? current.FinalScore : x.PredictedScore ?? 0m))
            })
            .OrderByDescending(x => x.Delta)
            .ThenByDescending(x => x.CurrentScore)
            .ThenBy(x => x.Movie.Title)
            .Take(12)
            .ToList();

        if (previewMovieId.HasValue || randomize)
        {
            var changedCandidates = rankedCandidates
                .Where(x => decimal.Round(x.Delta, 1) > 0m)
                .ToList();
            if (changedCandidates.Count > 0)
            {
                rankedCandidates = changedCandidates;
            }
        }

        var candidates = rankedCandidates
            .Select(x => x.Movie)
            .ToList();

        if (candidates.Count == 0)
        {
            return null;
        }

        return randomize
            ? candidates[Random.Shared.Next(candidates.Count)]
            : candidates[0];
    }

    private static bool HasMeaningfulPreviewDelta(
        Movie movie,
        IReadOnlyDictionary<int, RecommendationResult> currentResults,
        IReadOnlyDictionary<int, RecommendationResult> previewResults)
    {
        var currentScore = currentResults.TryGetValue(movie.Id, out var current)
            ? current.FinalScore
            : movie.PredictedScore ?? 0m;
        var previewScore = previewResults.TryGetValue(movie.Id, out var projected)
            ? projected.FinalScore
            : currentScore;

        return decimal.Round(Math.Abs(previewScore - currentScore), 1) > 0m;
    }
}
