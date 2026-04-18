using System.Globalization;
using System.Text;
using LocalMovieVault.Web.Data;
using LocalMovieVault.Web.Helpers;
using LocalMovieVault.Web.Services;
using LocalMovieVault.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LocalMovieVault.Web.Controllers;

public sealed class SettingsController : Controller
{
    private readonly AppDbContext _dbContext;
    private readonly AppUserPreferencesService _preferencesService;
    private readonly PersonalMatchService _personalMatchService;
    private readonly RecommendationPreviewService _previewService;
    private readonly AppEventLogService _appEventLogService;

    public SettingsController(
        AppDbContext dbContext,
        AppUserPreferencesService preferencesService,
        PersonalMatchService personalMatchService,
        RecommendationPreviewService previewService,
        AppEventLogService? appEventLogService = null)
    {
        _dbContext = dbContext;
        _preferencesService = preferencesService;
        _personalMatchService = personalMatchService;
        _previewService = previewService;
        _appEventLogService = appEventLogService ?? new AppEventLogService(dbContext);
    }

    [HttpGet]
    public async Task<IActionResult> Index(int? previewMovieId, bool randomize = false, CancellationToken cancellationToken = default)
    {
        var preferences = _preferencesService.Get();
        var viewModel = await _previewService.BuildViewModelAsync(preferences, previewMovieId, randomize, cancellationToken);
        return View(viewModel);
    }

    [HttpGet]
    public async Task<IActionResult> Diagnostics(CancellationToken cancellationToken)
    {
        var events = await _appEventLogService.GetRecentAsync(cancellationToken: cancellationToken);
        return View(new DiagnosticsViewModel
        {
            Events = events
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(AppSettingsViewModel model, CancellationToken cancellationToken)
    {
        var requestPath = HttpContext?.Request?.Path.ToString();
        var selectedTags = AppUserPreferences.NormalizeImportantTags(model.TastePrioritySelections);
        var preferences = _preferencesService.Get();
        preferences.ImportantTags = selectedTags;

        try
        {
            _preferencesService.Save(preferences);
            await _personalMatchService.RecalculateAsync(cancellationToken);
            await _appEventLogService.WriteSettingsEventAsync(
                "Settings.Save",
                "Success",
                $"Saved {selectedTags.Count} taste priorities.",
                requestPath,
                new
                {
                    SelectedTags = selectedTags,
                    PostedTagCount = model.TastePrioritySelections.Count
                },
                cancellationToken);

            if (TempData is not null)
            {
                TempData["StatusMessage"] = "Taste priorities applied. Predictions recalculated.";
            }

            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            await _appEventLogService.WriteSettingsEventAsync(
                "Settings.Save",
                "Error",
                "Settings save failed while applying taste priorities.",
                requestPath,
                new
                {
                    SelectedTags = selectedTags,
                    PostedTagCount = model.TastePrioritySelections.Count,
                    Exception = ex.Message
                },
                cancellationToken);
            throw;
        }
    }

    [HttpGet]
    public async Task<IActionResult> Preview([FromQuery] AppUserPreferences preferences, [FromQuery] List<string>? tastePrioritySelections, int? previewMovieId, CancellationToken cancellationToken)
    {
        preferences.ImportantTags = AppUserPreferences.NormalizeImportantTags(tastePrioritySelections ?? preferences.GetImportantTags());
        var viewModel = await _previewService.BuildViewModelAsync(preferences, previewMovieId, false, cancellationToken);
        return PartialView("_SettingsPreviewCard", viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ExportCsv(CancellationToken cancellationToken)
    {
        var movies = await _dbContext.Movies.OrderBy(x => x.Title).ToListAsync(cancellationToken);
        var lines = new List<string>
        {
            "Title,Year,Genres,Watched,Dismissed,UserScore,PredictedScore,TasteFit,PredictedLabel,PredictedReason,IMDb,ReviewBadge,DismissedReasons,ReasonTags"
        };

        var preferences = _preferencesService.Get();
        foreach (var movie in movies)
        {
            var row = new[]
            {
                Escape(movie.Title),
                Escape(movie.Year?.ToString() ?? string.Empty),
                Escape(movie.GenresCsv ?? string.Empty),
                Escape(movie.WatchedStatus.ToString()),
                Escape(movie.IsDismissed ? "Yes" : "No"),
                Escape(movie.UserRating?.ToString("0.#", CultureInfo.InvariantCulture) ?? string.Empty),
                Escape(movie.PredictedScore?.ToString("0.#", CultureInfo.InvariantCulture) ?? string.Empty),
                Escape(movie.PersonalMatchScore?.ToString("0.#", CultureInfo.InvariantCulture) ?? string.Empty),
                Escape(movie.PredictedLabel ?? string.Empty),
                Escape(movie.PredictedReason ?? string.Empty),
                Escape(movie.ImdbRating?.ToString("0.0", CultureInfo.InvariantCulture) ?? string.Empty),
                Escape(MovieStateHelper.GetReviewBadge(movie, preferences.DismissScoreThreshold) ?? string.Empty),
                Escape(movie.DismissedReasonTagsCsv ?? string.Empty),
                Escape(movie.ReasonTagsCsv ?? string.Empty)
            };
            lines.Add(string.Join(",", row));
        }

        var bytes = Encoding.UTF8.GetBytes(string.Join(Environment.NewLine, lines));
        return File(bytes, "text/csv", "mymoviedb-export.csv");
    }

    private static string Escape(string value)
        => "\"" + value.Replace("\"", "\"\"") + "\"";
}
