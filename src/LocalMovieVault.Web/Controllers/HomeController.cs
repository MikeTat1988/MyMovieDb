using LocalMovieVault.Web.Data;
using LocalMovieVault.Web.Helpers;
using LocalMovieVault.Web.Models;
using LocalMovieVault.Web.Services;
using LocalMovieVault.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LocalMovieVault.Web.Controllers;

public class HomeController : Controller
{
    private readonly AppDbContext _dbContext;
    private readonly AppUserPreferencesService _preferencesService;

    public HomeController(AppDbContext dbContext, AppUserPreferencesService preferencesService)
    {
        _dbContext = dbContext;
        _preferencesService = preferencesService;
    }

    public async Task<IActionResult> Index(string? genre, CancellationToken cancellationToken)
    {
        var preferences = _preferencesService.Get();
        genre ??= preferences.DefaultGenre;

        var movies = await _dbContext.Movies
            .OrderBy(x => x.Title)
            .ToListAsync(cancellationToken);

        var suggestionPool = movies
            .Where(x => MovieStateHelper.IsRecommendationCandidate(x, preferences.DismissScoreThreshold))
            .ToList();

        var filteredSuggestionPool = suggestionPool
            .Where(x => GenreHelper.MatchesGenre(x, genre))
            .ToList();

        if (filteredSuggestionPool.Count == 0)
        {
            filteredSuggestionPool = suggestionPool;
        }

        var model = new HomeDashboardViewModel
        {
            TotalCount = movies.Count,
            SelectedGenre = genre,
            DefaultGenre = preferences.DefaultGenre,
            Genres = GenreHelper.CollectGenres(movies),
            TopMatches = filteredSuggestionPool
                .OrderByDescending(x => x.PredictedScore ?? x.PersonalMatchScore ?? 0)
                .ThenByDescending(x => x.ImdbRating ?? 0)
                .ThenBy(x => x.Title)
                .Take(4)
                .ToList()
        };

        return View("BestMatch", model);
    }

    public async Task<IActionResult> Surprise(CancellationToken cancellationToken)
    {
        var preferences = _preferencesService.Get();
        var pool = await _dbContext.Movies
            .Where(x => x.WatchedStatus != WatchedStatus.Watched && !x.IsDismissed)
            .ToListAsync(cancellationToken);

        var eligible = pool
            .Where(x => !MovieStateHelper.NeedsReview(x, preferences.DismissScoreThreshold))
            .ToList();

        var pick = eligible.Count == 0
            ? null
            : eligible[Random.Shared.Next(eligible.Count)];

        return View("Surprise", new SurpriseMovieViewModel
        {
            Pick = pick,
            EligibleCount = eligible.Count,
            TotalCount = pool.Count
        });
    }

    public IActionResult Error()
    {
        return View();
    }
}
