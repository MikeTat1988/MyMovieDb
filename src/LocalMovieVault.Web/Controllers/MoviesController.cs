using LocalMovieVault.Web.Contracts;
using LocalMovieVault.Web.Data;
using LocalMovieVault.Web.Helpers;
using LocalMovieVault.Web.Models;
using LocalMovieVault.Web.Services;
using LocalMovieVault.Web.Services.Recommendations;
using LocalMovieVault.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace LocalMovieVault.Web.Controllers;

public class MoviesController : Controller
{
    private const decimal ImmediateMismatchSuggestionThreshold = 40m;
    private const int RepeatedMismatchPromptMarks = 3;
    private const int MismatchCooldownRatingCount = 5;
    private static readonly TimeSpan MismatchCooldownDuration = TimeSpan.FromDays(14);
    private static readonly JsonSerializerOptions RecommendationContextJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly AppDbContext _dbContext;
    private readonly IMovieMetadataService _metadataService;
    private readonly MovieUpsertService _movieUpsertService;
    private readonly PersonalMatchService _personalMatchService;
    private readonly MetadataBackfillService _metadataBackfillService;
    private readonly AppUserPreferencesService _preferencesService;
    private readonly AppEventLogService _appEventLogService;
    private readonly DeterministicRecommendationEngine _recommendationEngine;

    public MoviesController(
        AppDbContext dbContext,
        IMovieMetadataService metadataService,
        MovieUpsertService movieUpsertService,
        PersonalMatchService personalMatchService,
        MetadataBackfillService metadataBackfillService,
        AppUserPreferencesService preferencesService,
        IRecommendationEngine recommendationEngine,
        AppEventLogService? appEventLogService = null)
    {
        _dbContext = dbContext;
        _metadataService = metadataService;
        _movieUpsertService = movieUpsertService;
        _personalMatchService = personalMatchService;
        _metadataBackfillService = metadataBackfillService;
        _preferencesService = preferencesService;
        _recommendationEngine = (DeterministicRecommendationEngine)recommendationEngine;
        _appEventLogService = appEventLogService ?? new AppEventLogService(dbContext);
    }

    [HttpGet]
    public async Task<IActionResult> Index(string section = "not-watched", string? query = null, string? genre = null, string sortBy = "personal", CancellationToken cancellationToken = default)
    {
        var preferences = _preferencesService.Get();
        var allMovies = await _dbContext.Movies.ToListAsync(cancellationToken);
        var hasQuery = !string.IsNullOrWhiteSpace(query);
        var genres = GenreHelper.CollectGenres(allMovies);
        var notWatchedCount = allMovies.Count(x => x.WatchedStatus != WatchedStatus.Watched && !x.IsDismissed && !MovieStateHelper.NeedsReview(x, preferences.DismissScoreThreshold));
        var watchedCount = allMovies.Count(x => x.WatchedStatus == WatchedStatus.Watched && !x.IsDismissed);
        var reviewCount = allMovies.Count(x => MovieStateHelper.NeedsReview(x, preferences.DismissScoreThreshold));
        var dismissedCount = allMovies.Count(x => x.IsDismissed);
        var wowCount = allMovies.Count(x => x.IsWowPick);
        var wowLimit = WowPickHelper.GetWowLimit(allMovies.Count, preferences);

        IEnumerable<Movie> movies = allMovies;

        if (!string.IsNullOrWhiteSpace(query))
        {
            var q = query.Trim();
            movies = movies.Where(x =>
                x.Title.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrWhiteSpace(x.OriginalTitle) && x.OriginalTitle.Contains(q, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(x.GenresCsv) && x.GenresCsv.Contains(q, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(x.Notes) && x.Notes.Contains(q, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(x.TagsCsv) && x.TagsCsv.Contains(q, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(x.PredictedReason) && x.PredictedReason.Contains(q, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(x.Director) && x.Director.Contains(q, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(x.Actors) && x.Actors.Contains(q, StringComparison.OrdinalIgnoreCase)));
        }

        if (!string.IsNullOrWhiteSpace(genre))
        {
            movies = movies.Where(x => GenreHelper.MatchesGenre(x, genre));
        }

        if (!hasQuery)
        {
            movies = section switch
            {
                "watched" => movies.Where(x => x.WatchedStatus == WatchedStatus.Watched && !x.IsDismissed),
                "review" => movies.Where(x => MovieStateHelper.NeedsReview(x, preferences.DismissScoreThreshold)),
                "dismissed" => movies.Where(x => x.IsDismissed),
                "wow" => movies.Where(x => x.IsWowPick && !x.IsDismissed),
                _ => movies.Where(x => x.WatchedStatus != WatchedStatus.Watched && !x.IsDismissed && !MovieStateHelper.NeedsReview(x, preferences.DismissScoreThreshold))
            };
        }

        var orderedMovies = sortBy switch
        {
            "title" => movies.OrderBy(x => x.Title).ToList(),
            "imdb" => movies.OrderByDescending(x => x.ImdbRating ?? 0).ThenBy(x => x.Title).ToList(),
            "user" => movies.OrderByDescending(x => x.UserRating ?? 0).ThenBy(x => x.Title).ToList(),
            "recent" => movies.OrderByDescending(x => x.UpdatedUtc).ToList(),
            _ => movies.OrderByDescending(GetRecommendationSortScore).ThenByDescending(x => x.ImdbRating ?? 0).ThenBy(x => x.Title).ToList()
        };

        var movieList = section == "review"
            ? orderedMovies
                .OrderBy(x => MovieStateHelper.GetReviewBadge(x, preferences.DismissScoreThreshold) == "Needs tags" ? 0 : 1)
                .ThenByDescending(GetRecommendationSortScore)
                .ToList()
            : orderedMovies;

        var model = new MovieListViewModel
        {
            TotalCount = allMovies.Count,
            NotWatchedCount = notWatchedCount,
            WatchedCount = watchedCount,
            ReviewCount = reviewCount,
            DismissedCount = dismissedCount,
            WowCount = wowCount,
            WowLimit = wowLimit,
            Section = section,
            Query = query,
            Genre = genre,
            SortBy = sortBy,
            Genres = genres,
            Movies = movieList,
            DismissScoreThreshold = preferences.DismissScoreThreshold
        };

        return View("Library", model);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var movie = await _dbContext.Movies.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (movie is null)
        {
            return NotFound();
        }

        var preferences = _preferencesService.Get();
        var referenceMovie = await ResolveReferenceMovieAsync(movie, cancellationToken);
        var wowCount = await _dbContext.Movies.CountAsync(x => x.IsWowPick, cancellationToken);
        var wowLimit = WowPickHelper.GetWowLimit(await _dbContext.Movies.CountAsync(cancellationToken), preferences);
        return View("MovieDetails", MovieDetailsViewModel.Create(movie, preferences.DismissScoreThreshold, wowCount, wowLimit, referenceMovie));
    }

    private async Task<Movie?> ResolveReferenceMovieAsync(Movie movie, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(movie.RecommendationContextJson))
        {
            return null;
        }

        RecommendationContext? context;
        try
        {
            context = JsonSerializer.Deserialize<RecommendationContext>(movie.RecommendationContextJson, RecommendationContextJsonOptions);
        }
        catch
        {
            context = null;
        }

        var referenceSummary = context?.SimilarToLiked
            .OrderByDescending(x => x.SimilarityScore)
            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.Title));
        var referenceTitle = referenceSummary?.Title;
        if (string.IsNullOrWhiteSpace(referenceTitle))
        {
            return null;
        }

        var normalizedTitle = TitleNormalizer.Normalize(referenceTitle);
        var candidates = await _dbContext.Movies
            .Where(x => x.Id != movie.Id && x.NormalizedTitle == normalizedTitle)
            .ToListAsync(cancellationToken);

        var referenceMovie = candidates
            .OrderByDescending(x => x.WatchedStatus == WatchedStatus.Watched)
            .ThenByDescending(x => x.UserRating ?? decimal.MinValue)
            .ThenByDescending(x => x.PredictedScore ?? decimal.MinValue)
            .FirstOrDefault();

        return referenceMovie is not null && IsReferenceMovieStrongEnough(movie, referenceMovie, context!, referenceSummary!)
            ? referenceMovie
            : null;
    }

    private static bool IsReferenceMovieStrongEnough(Movie movie, Movie referenceMovie, RecommendationContext context, SimilarMovieSummary referenceSummary)
    {
        if (referenceSummary.SimilarityScore >= 24m)
        {
            return true;
        }

        var currentGenres = RecommendationViewHelper.SplitCsv(movie.GenresCsv)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var referenceGenres = RecommendationViewHelper.SplitCsv(referenceMovie.GenresCsv)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var sharedGenres = currentGenres.Intersect(referenceGenres, StringComparer.OrdinalIgnoreCase).Count();

        var currentSignals = ExtractSemanticSignals(context);
        var referenceSignals = RecommendationViewHelper.SplitCsv(referenceMovie.NormalizedTagsCsv ?? referenceMovie.ReasonTagsCsv)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var sharedSignals = currentSignals.Intersect(referenceSignals, StringComparer.OrdinalIgnoreCase).Count();

        if (IsMixedComedyMismatch(currentGenres, referenceGenres) && sharedSignals == 0)
        {
            return false;
        }

        if (sharedSignals > 0 && referenceSummary.SimilarityScore >= 12m)
        {
            return true;
        }

        return sharedGenres >= 2 && referenceSummary.SimilarityScore >= 18m;
    }

    private static HashSet<string> ExtractSemanticSignals(RecommendationContext context)
    {
        return context.PositiveFactors
            .Where(x => x.Label.StartsWith("tag:", StringComparison.OrdinalIgnoreCase)
                || x.Label.StartsWith("priority:", StringComparison.OrdinalIgnoreCase))
            .Select(x => x.Label)
            .Select(label => label.Split(':', 2)[1].Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsMixedComedyMismatch(IReadOnlySet<string> currentGenres, IReadOnlySet<string> referenceGenres)
    {
        var currentIsComedyHybrid = currentGenres.Contains("Comedy") && currentGenres.Count > 1;
        var referenceIsComedyHybrid = referenceGenres.Contains("Comedy") && referenceGenres.Count > 1;
        return currentIsComedyHybrid != referenceIsComedyHybrid;
    }

    [HttpGet]
    public IActionResult Add()
    {
        return View(new AddMoviePageViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Lookup(AddMoviePageViewModel model, CancellationToken cancellationToken)
    {
        var candidates = await _metadataService.SearchCandidatesAsync(model.LookupTitle ?? string.Empty, model.LookupYear, 10, cancellationToken);
        model.Candidates = candidates.ToList();

        if (model.Candidates.Count == 0)
        {
            var result = await _metadataService.LookupByTitleAsync(model.LookupTitle ?? string.Empty, model.LookupYear, cancellationToken);
            if (!result.Success)
            {
                model.ShowSavePopup = false;
                model.LookupMessage = result.ErrorMessage;
                model.Movie.Title = model.LookupTitle ?? string.Empty;
                model.Movie.Year = model.LookupYear;
                return View("Add", model);
            }

            model.LookupMessage = "Found one match. Check and save.";
            model.ShowSavePopup = true;
            model.Movie = MapLookupResult(result);
            model.SelectedImdbId = result.ExternalId;
            return View("Add", model);
        }

        var selected = model.Candidates.First();
        var selectedResult = await _metadataService.LookupByImdbIdAsync(selected.ImdbId, cancellationToken);
        if (!selectedResult.Success)
        {
            model.ShowSavePopup = false;
            model.LookupMessage = selectedResult.ErrorMessage ?? "Could not open the selected match.";
            return View("Add", model);
        }

        model.LookupMessage = model.Candidates.Count > 1
            ? "Found several matches. Pick the right one or save this one directly."
            : "Found one match. Check and save.";
        model.ShowSavePopup = true;
        model.Movie = MapLookupResult(selectedResult);
        model.SelectedImdbId = selected.ImdbId;
        return View("Add", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChooseCandidate(string imdbId, string? lookupTitle, int? lookupYear, CancellationToken cancellationToken)
    {
        var model = new AddMoviePageViewModel
        {
            LookupTitle = lookupTitle,
            LookupYear = lookupYear
        };

        model.Candidates = (await _metadataService.SearchCandidatesAsync(lookupTitle ?? string.Empty, lookupYear, 10, cancellationToken)).ToList();

        var result = await _metadataService.LookupByImdbIdAsync(imdbId, cancellationToken);
        if (!result.Success)
        {
            model.LookupMessage = result.ErrorMessage ?? "Could not open the selected match.";
            model.ShowSavePopup = false;
            return View("Add", model);
        }

        model.LookupMessage = model.Candidates.Count > 1
            ? "Match selected. Check and save."
            : "Match found. Check and save.";
        model.ShowSavePopup = true;
        model.Movie = MapLookupResult(result);
        model.SelectedImdbId = imdbId;
        return View("Add", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PreviewEstimate([Bind(Prefix = "Movie")] MovieEditViewModel model, string? lookupTitle, int? lookupYear, string? selectedImdbId, CancellationToken cancellationToken)
    {
        var addModel = await BuildAddPageModelAsync(model, lookupTitle, lookupYear, selectedImdbId, cancellationToken);
        addModel.LookupMessage = "Estimated match preview ready.";
        addModel.ShowEstimateModal = true;
        addModel.EstimatePreview = await BuildEstimatePreviewAsync(model, cancellationToken);
        return View("Add", addModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CompleteWatch(int id, PersonalVerdict primaryVerdict, List<string>? reasonTags, string? returnUrl, CancellationToken cancellationToken)
    {
        var movie = await _dbContext.Movies.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (movie is null)
        {
            await _appEventLogService.WriteMovieEventAsync(
                "Movie.CompleteWatch",
                "Missing",
                $"Complete watch skipped: movie #{id} was not found.",
                null,
                Request.Path.ToString(),
                new { MovieId = id, PrimaryVerdict = primaryVerdict.ToString(), ReasonTags = reasonTags ?? [] },
                cancellationToken);
            return AjaxOrRedirect(false, null, null, null, null, returnUrl);
        }

        movie.PrimaryVerdict = primaryVerdict;
        movie.UserGrade = RecommendationViewHelper.MapVerdictToGrade(primaryVerdict);
        movie.UserRating = RecommendationViewHelper.MapVerdictToUserRating(primaryVerdict);
        movie.WatchedStatus = WatchedStatus.Watched;
        movie.ReasonTagsCsv = RecommendationViewHelper.JoinCsv((reasonTags ?? []).Take(RecommendationViewHelper.MaxReasonTags));
        movie.NormalizedTagsCsv = movie.ReasonTagsCsv;
        movie.NeedsTagReview = RecommendationViewHelper.SplitCsv(movie.NormalizedTagsCsv).Count() < RecommendationViewHelper.GetMinimumReasonTags(movie.UserGrade);
        movie.IsDismissed = false;
        movie.DismissedUtc = null;
        movie.DismissedReasonTagsCsv = null;

        await _dbContext.SaveChangesAsync(cancellationToken);
        await _personalMatchService.RecalculateAsync(cancellationToken);
        await _dbContext.Entry(movie).ReloadAsync(cancellationToken);
        await _appEventLogService.WriteMovieEventAsync(
            "Movie.CompleteWatch",
            "Success",
            $"Saved watch feedback for '{movie.Title}'.",
            movie,
            Request.Path.ToString(),
            new
            {
                movie.WatchedStatus,
                movie.PrimaryVerdict,
                movie.UserGrade,
                movie.UserRating,
                movie.NeedsTagReview,
                ReasonTags = RecommendationViewHelper.SplitCsv(movie.ReasonTagsCsv).ToArray()
            },
            cancellationToken);

        var preferences = _preferencesService.Get();
        preferences.CompletedRatingCount += 1;
        var mismatchValue = movie.PredictedScore.HasValue
            ? Math.Abs(movie.PredictedScore.Value - movie.UserRating.Value)
            : 0m;
        var showMismatch = mismatchValue >= preferences.PredictionMismatchThreshold;
        var mismatchSuggestion = showMismatch
            ? UpdateMismatchStateAndBuildPrompt(movie, preferences, mismatchValue)
            : null;
        _preferencesService.Save(preferences);

        if (IsAjaxRequest())
        {
            var redirectUrl = ShouldRedirectToDetails(returnUrl)
                ? Url.Action(nameof(Details), new { id = movie.Id })
                : null;

            return Json(new
            {
                success = true,
                movedToWatched = true,
                redirectUrl,
                showMismatchPopup = showMismatch,
                mismatchValue = mismatchValue.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture),
                predictedScore = movie.PredictedScore?.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture),
                userScore = movie.UserRating.ToString(),
                mismatchSuggestion = mismatchSuggestion is null
                    ? null
                    : new
                    {
                        kind = mismatchSuggestion.Kind,
                        value = mismatchSuggestion.Value,
                        label = mismatchSuggestion.Label,
                        direction = mismatchSuggestion.Direction,
                        prompt = mismatchSuggestion.Prompt,
                        marks = mismatchSuggestion.Marks,
                        mismatchValue = mismatchSuggestion.MismatchValue.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)
                    },
                message = showMismatch
                    ? $"Prediction gap: app {movie.PredictedScore:0.#} vs your {movie.UserRating:0.#}"
                    : string.Empty
            });
        }

        TempData["StatusMessage"] = "Watch feedback saved.";
        return RedirectToLocalOr(returnUrl, nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApplyMismatchPreferenceSuggestion(
        string kind,
        string value,
        string? label,
        string direction,
        string response,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(kind) || string.IsNullOrWhiteSpace(value))
        {
            return Json(new { success = false, message = "Missing mismatch preference factor." });
        }

        var preferences = _preferencesService.Get();
        var state = GetOrCreateMismatchFactor(preferences, kind, value, label ?? value);

        if (string.Equals(response, "accept", StringComparison.OrdinalIgnoreCase))
        {
            var adjustment = string.Equals(direction, "less", StringComparison.OrdinalIgnoreCase) ? -0.75m : 0.75m;
            var applied = ApplyPreferenceAdjustment(preferences, kind, value, adjustment);
            ResetMismatchMarks(state);
            state.CooldownUntilUtc = null;
            state.CooldownUntilRatingCount = 0;
            _preferencesService.Save(preferences);
            await _personalMatchService.RecalculateAsync(cancellationToken);

            return Json(new
            {
                success = true,
                recalculated = true,
                applied,
                message = applied
                    ? $"Updated your preferences to show {direction} {label ?? value}."
                    : "Could not apply that preference suggestion."
            });
        }

        if (string.Equals(response, "reject", StringComparison.OrdinalIgnoreCase)
            || string.Equals(response, "dismiss", StringComparison.OrdinalIgnoreCase)
            || string.Equals(response, "no", StringComparison.OrdinalIgnoreCase))
        {
            ResetMismatchMarks(state);
            state.CooldownUntilUtc = DateTime.UtcNow.Add(MismatchCooldownDuration);
            state.CooldownUntilRatingCount = preferences.CompletedRatingCount + MismatchCooldownRatingCount;
            _preferencesService.Save(preferences);
            return Json(new { success = true, recalculated = false, dismissed = true });
        }

        if (string.Equals(response, "cancel", StringComparison.OrdinalIgnoreCase))
        {
            return Json(new { success = true, recalculated = false, cancelled = true });
        }

        return Json(new { success = false, message = "Unknown mismatch response." });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> QueueForReview(int id, string? returnUrl, CancellationToken cancellationToken)
    {
        var movie = await _dbContext.Movies.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (movie is null)
        {
            await _appEventLogService.WriteMovieEventAsync(
                "Movie.QueueForReview",
                "Missing",
                $"Queue for review skipped: movie #{id} was not found.",
                null,
                Request.Path.ToString(),
                new { MovieId = id },
                cancellationToken);
            return AjaxOrRedirect(false, null, null, null, null, returnUrl);
        }

        movie.NeedsTagReview = true;
        await _dbContext.SaveChangesAsync(cancellationToken);
        await _appEventLogService.WriteMovieEventAsync(
            "Movie.QueueForReview",
            "Success",
            $"Moved '{movie.Title}' into review.",
            movie,
            Request.Path.ToString(),
            new { movie.NeedsTagReview, movie.WatchedStatus },
            cancellationToken);

        var reviewUrl = Url.Action(nameof(Index), new { section = "review" }) ?? "/Movies?section=review";

        if (IsAjaxRequest())
        {
            return Json(new
            {
                success = true,
                queuedForReview = true,
                redirectUrl = reviewUrl
            });
        }

        TempData["StatusMessage"] = "Moved to review.";
        return Redirect(reviewUrl);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Dismiss(int id, List<string>? reasonTags, string? returnUrl, CancellationToken cancellationToken)
    {
        var movie = await _dbContext.Movies.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (movie is null)
        {
            await _appEventLogService.WriteMovieEventAsync(
                "Movie.Dismiss",
                "Missing",
                $"Dismiss skipped: movie #{id} was not found.",
                null,
                Request.Path.ToString(),
                new { MovieId = id, ReasonTags = reasonTags ?? [] },
                cancellationToken);
            return AjaxOrRedirect(false, null, null, null, null, returnUrl);
        }

        movie.IsDismissed = true;
        movie.IsWowPick = false;
        movie.DismissedUtc = DateTime.UtcNow;
        movie.DismissedReasonTagsCsv = RecommendationViewHelper.JoinCsv((reasonTags ?? []).Take(RecommendationViewHelper.MaxReasonTags));

        await _dbContext.SaveChangesAsync(cancellationToken);
        await _appEventLogService.WriteMovieEventAsync(
            "Movie.Dismiss",
            "Success",
            $"Dismissed '{movie.Title}'.",
            movie,
            Request.Path.ToString(),
            new
            {
                movie.DismissedUtc,
                ReasonTags = RecommendationViewHelper.SplitCsv(movie.DismissedReasonTagsCsv).ToArray()
            },
            cancellationToken);

        if (IsAjaxRequest())
        {
            return Json(new { success = true, dismissed = true });
        }

        TempData["StatusMessage"] = "Movie dismissed.";
        return RedirectToLocalOr(returnUrl, nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Restore(int id, string? returnUrl, CancellationToken cancellationToken)
    {
        var movie = await _dbContext.Movies.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (movie is null)
        {
            await _appEventLogService.WriteMovieEventAsync(
                "Movie.Restore",
                "Missing",
                $"Restore skipped: movie #{id} was not found.",
                null,
                Request.Path.ToString(),
                new { MovieId = id },
                cancellationToken);
            return AjaxOrRedirect(false, null, null, null, null, returnUrl);
        }

        movie.IsDismissed = false;
        movie.DismissedUtc = null;
        movie.DismissedReasonTagsCsv = null;
        await _dbContext.SaveChangesAsync(cancellationToken);
        await _appEventLogService.WriteMovieEventAsync(
            "Movie.Restore",
            "Success",
            $"Restored '{movie.Title}' from dismissed.",
            movie,
            Request.Path.ToString(),
            new { movie.IsDismissed, movie.WatchedStatus },
            cancellationToken);

        if (IsAjaxRequest())
        {
            return Json(new { success = true, restored = true });
        }

        TempData["StatusMessage"] = "Movie restored.";
        return RedirectToLocalOr(returnUrl, nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind(Prefix = "Movie")] MovieEditViewModel model, string? lookupTitle, int? lookupYear, string? selectedImdbId, PersonalVerdict? addVerdict, List<string>? addReasonTags, bool queueForReview, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            var addModel = await BuildAddPageModelAsync(model, lookupTitle, lookupYear, selectedImdbId, cancellationToken);
            addModel.LookupMessage = "Please check the required fields.";
            return View("Add", addModel);
        }

        ApplyAddReviewState(model, addVerdict, addReasonTags, queueForReview);

        var entity = model.ToEntity();
        var normalizedTitle = TitleNormalizer.Normalize(entity.Title);
        await _movieUpsertService.UpsertImportedAsync(entity, cancellationToken);

        await _personalMatchService.RecalculateAsync(cancellationToken);

        var savedMovie = await _dbContext.Movies
            .Where(x => x.NormalizedTitle == normalizedTitle && x.MediaType == entity.MediaType)
            .OrderByDescending(x => x.Year == entity.Year)
            .ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (savedMovie is null)
        {
            return RedirectToAction(nameof(Index));
        }

        TempData["StatusMessage"] = "Movie was added to the library.";
        if (queueForReview)
        {
            return RedirectToAction(nameof(Index), new { section = "review" });
        }

        return RedirectToAction(nameof(Index), new { section = model.WatchedStatus == WatchedStatus.Watched ? "watched" : "not-watched" });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var movie = await _dbContext.Movies.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (movie is null)
        {
            return NotFound();
        }

        return View(MovieEditViewModelMapper.FromEntity(movie));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(MovieEditViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var existing = await _dbContext.Movies.FirstOrDefaultAsync(x => x.Id == model.Id, cancellationToken);
        if (existing is null)
        {
            return NotFound();
        }

        NormalizeManualReviewFields(model);

        await _movieUpsertService.UpdateManualAsync(existing, model.ToEntity(), cancellationToken);
        await _personalMatchService.RecalculateAsync(cancellationToken);

        TempData["StatusMessage"] = "Movie updated.";
        return RedirectToAction(nameof(Details), new { id = existing.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, string? returnUrl, CancellationToken cancellationToken)
    {
        var movie = await _dbContext.Movies.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (movie is null)
        {
            if (IsAjaxRequest())
            {
                return Json(new { success = false });
            }
            return RedirectToAction(nameof(Index));
        }

        _dbContext.Movies.Remove(movie);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await _personalMatchService.RecalculateAsync(cancellationToken);

        if (IsAjaxRequest())
        {
            return Json(new { success = true, deletedId = id });
        }

        TempData["StatusMessage"] = "Movie deleted.";
        return RedirectToDeleteReturnUrl(returnUrl);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleWatched(int id, string? returnUrl, CancellationToken cancellationToken)
    {
        var movie = await _dbContext.Movies.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (movie is null)
        {
            await _appEventLogService.WriteMovieEventAsync(
                "Movie.ToggleWatched",
                "Missing",
                $"Toggle watched skipped: movie #{id} was not found.",
                null,
                Request.Path.ToString(),
                new { MovieId = id },
                cancellationToken);
            return AjaxOrRedirect(false, null, null, null, null, returnUrl);
        }

        if (movie.WatchedStatus == WatchedStatus.Watched)
        {
            movie.WatchedStatus = WatchedStatus.NotWatched;
            movie.UserRating = null;
            movie.PrimaryVerdict = null;
            movie.UserGrade = null;
            movie.ReasonTagsCsv = null;
            movie.NormalizedTagsCsv = null;
            movie.NeedsTagReview = false;
            movie.IsWowPick = false;
        }
        else
        {
            movie.WatchedStatus = WatchedStatus.Watched;
            movie.PrimaryVerdict ??= PersonalVerdict.Okay;
            movie.UserGrade ??= RecommendationViewHelper.MapVerdictToGrade(movie.PrimaryVerdict.Value);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await _personalMatchService.RecalculateAsync(cancellationToken);
        await _dbContext.Entry(movie).ReloadAsync(cancellationToken);
        await _appEventLogService.WriteMovieEventAsync(
            "Movie.ToggleWatched",
            "Success",
            $"Toggled watched state for '{movie.Title}' to {movie.WatchedStatus}.",
            movie,
            Request.Path.ToString(),
            new
            {
                movie.WatchedStatus,
                movie.PrimaryVerdict,
                movie.UserGrade,
                movie.UserRating
            },
            cancellationToken);

        return AjaxOrRedirect(
            true,
            movie.WatchedStatus == WatchedStatus.Watched,
            movie.UserRating,
            movie.PrimaryVerdict,
            RecommendationViewHelper.SplitCsv(movie.ReasonTagsCsv).ToArray(),
            returnUrl,
            movie.PersonalMatchScore,
            movie.PredictedScore,
            movie.PredictedLabel,
            movie.PredictedReason);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleWow(int id, string? returnUrl, CancellationToken cancellationToken)
    {
        var movie = await _dbContext.Movies.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (movie is null)
        {
            return WowAjaxOrRedirect(false, "Movie not found.", false, 0, 0, returnUrl);
        }

        var preferences = _preferencesService.Get();
        var totalMovieCount = await _dbContext.Movies.CountAsync(cancellationToken);
        var wowLimit = WowPickHelper.GetWowLimit(totalMovieCount, preferences);
        var wowCount = await _dbContext.Movies.CountAsync(x => x.IsWowPick, cancellationToken);

        if (!movie.IsWowPick && !WowPickHelper.CanAssignWow(movie))
        {
            return WowAjaxOrRedirect(false, "Wow is only available for watched movies with a completed review.", movie.IsWowPick, wowCount, wowLimit, returnUrl);
        }

        if (!movie.IsWowPick && wowCount >= wowLimit)
        {
            return WowAjaxOrRedirect(false, $"Wow limit reached ({wowCount}/{wowLimit}). Remove one wow pick before adding another.", movie.IsWowPick, wowCount, wowLimit, returnUrl);
        }

        movie.IsWowPick = !movie.IsWowPick;
        await _dbContext.SaveChangesAsync(cancellationToken);
        await _personalMatchService.RecalculateAsync(cancellationToken);
        await _dbContext.Entry(movie).ReloadAsync(cancellationToken);

        wowCount = await _dbContext.Movies.CountAsync(x => x.IsWowPick, cancellationToken);

        await _appEventLogService.WriteMovieEventAsync(
            "Movie.ToggleWow",
            "Success",
            $"{(movie.IsWowPick ? "Marked" : "Removed")} wow pick for '{movie.Title}'.",
            movie,
            Request.Path.ToString(),
            new { movie.IsWowPick, WowCount = wowCount, WowLimit = wowLimit },
            cancellationToken);

        return WowAjaxOrRedirect(true, movie.IsWowPick ? "Wow pick saved." : "Wow pick removed.", movie.IsWowPick, wowCount, wowLimit, returnUrl);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetTaste(int id, PersonalVerdict? primaryVerdict, List<string>? reasonTags, string? returnUrl, CancellationToken cancellationToken)
    {
        var movie = await _dbContext.Movies.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (movie is null)
        {
            return AjaxOrRedirect(false, null, null, null, null, returnUrl);
        }

        movie.PrimaryVerdict = primaryVerdict;
        movie.UserGrade = primaryVerdict.HasValue ? RecommendationViewHelper.MapVerdictToGrade(primaryVerdict.Value) : movie.UserGrade;
        movie.ReasonTagsCsv = RecommendationViewHelper.JoinCsv((reasonTags ?? []).Take(RecommendationViewHelper.MaxReasonTags));
        movie.NormalizedTagsCsv = movie.ReasonTagsCsv;
        movie.NeedsTagReview = primaryVerdict.HasValue
            && RecommendationViewHelper.SplitCsv(movie.ReasonTagsCsv).Count() < RecommendationViewHelper.GetMinimumReasonTags(movie.UserGrade);

        if (primaryVerdict.HasValue)
        {
            movie.WatchedStatus = WatchedStatus.Watched;
            movie.UserRating = RecommendationViewHelper.MapVerdictToUserRating(primaryVerdict.Value);
        }

        if (!primaryVerdict.HasValue && movie.WatchedStatus != WatchedStatus.Watched)
        {
            movie.ReasonTagsCsv = null;
            movie.NormalizedTagsCsv = null;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await _personalMatchService.RecalculateAsync(cancellationToken);
        await _dbContext.Entry(movie).ReloadAsync(cancellationToken);

        return AjaxOrRedirect(
            true,
            movie.WatchedStatus == WatchedStatus.Watched,
            movie.UserRating,
            movie.PrimaryVerdict,
            RecommendationViewHelper.SplitCsv(movie.ReasonTagsCsv).ToArray(),
            returnUrl,
            movie.PersonalMatchScore,
            movie.PredictedScore,
            movie.PredictedLabel,
            movie.PredictedReason);
    }

    [HttpGet]
    public async Task<IActionResult> Random(string? genre, string watched = "all", CancellationToken cancellationToken = default)
    {
        var movies = await _dbContext.Movies.ToListAsync(cancellationToken);
        IEnumerable<Movie> query = movies;

        if (!string.IsNullOrWhiteSpace(genre))
        {
            query = query.Where(x => GenreHelper.MatchesGenre(x, genre));
        }

        query = watched switch
        {
            "watched" => query.Where(x => x.WatchedStatus == WatchedStatus.Watched),
            "unwatched" => query.Where(x => x.WatchedStatus != WatchedStatus.Watched),
            _ => query
        };

        var movieList = query.ToList();
        if (movieList.Count == 0)
        {
            TempData["StatusMessage"] = "Nothing found for the current filter.";
            return RedirectToAction(nameof(Index));
        }

        var randomMovie = movieList[global::System.Random.Shared.Next(movieList.Count)];
        return RedirectToAction(nameof(Details), new { id = randomMovie.Id });
    }

    [HttpGet]
    public async Task<IActionResult> EnrichMissing(int take = 80, CancellationToken cancellationToken = default)
    {
        var result = await _metadataBackfillService.EnrichMissingAsync(take, cancellationToken);

        await _personalMatchService.RecalculateAsync(cancellationToken);

        TempData["StatusMessage"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    private MismatchSuggestionPayload? UpdateMismatchStateAndBuildPrompt(Movie movie, AppUserPreferences preferences, decimal mismatchValue)
    {
        var direction = movie.UserRating.GetValueOrDefault() >= movie.PredictedScore.GetValueOrDefault()
            ? "more"
            : "less";
        var factor = SelectMismatchFactor(movie, preferences);
        if (factor is null)
        {
            return null;
        }

        var state = GetOrCreateMismatchFactor(preferences, factor.Value.Kind, factor.Value.Value, factor.Value.Label);
        if (state.CooldownUntilUtc.HasValue && state.CooldownUntilUtc.Value > DateTime.UtcNow)
        {
            return null;
        }

        if (state.CooldownUntilRatingCount > preferences.CompletedRatingCount)
        {
            return null;
        }

        var markDelta = mismatchValue >= ImmediateMismatchSuggestionThreshold ? 2 : 1;
        if (string.Equals(direction, "less", StringComparison.OrdinalIgnoreCase))
        {
            state.NegativeMarks += markDelta;
        }
        else
        {
            state.PositiveMarks += markDelta;
        }

        state.Kind = factor.Value.Kind;
        state.Value = factor.Value.Value;
        state.Label = factor.Value.Label;

        var marks = string.Equals(direction, "less", StringComparison.OrdinalIgnoreCase)
            ? state.NegativeMarks
            : state.PositiveMarks;
        var shouldPrompt = mismatchValue >= ImmediateMismatchSuggestionThreshold || marks >= RepeatedMismatchPromptMarks;
        if (!shouldPrompt)
        {
            return null;
        }

        var prompt = string.Equals(direction, "less", StringComparison.OrdinalIgnoreCase)
            ? $"You disliked this more than expected. Use this as an extra signal to show fewer {factor.Value.Label} picks in future recommendations?"
            : $"You liked this much more than expected. Use this as an extra signal to show more {factor.Value.Label} picks in future recommendations?";

        return new MismatchSuggestionPayload(
            factor.Value.Kind,
            factor.Value.Value,
            factor.Value.Label,
            direction,
            prompt,
            marks,
            mismatchValue);
    }

    private static (string Kind, string Value, string Label)? SelectMismatchFactor(Movie movie, AppUserPreferences preferences)
    {
        var genres = RecommendationViewHelper.SplitCsv(movie.GenresCsv).ToList();
        if (genres.Count > 0)
        {
            var genre = genres
                .OrderByDescending(x => GetExistingMismatchMarks(preferences, "genre", x))
                .ThenBy(x => x)
                .First();
            return ("genre", genre, genre);
        }

        var directors = RecommendationViewHelper.SplitCsv(movie.Director).ToList();
        if (directors.Count > 0)
        {
            var director = directors
                .OrderByDescending(x => GetExistingMismatchMarks(preferences, "director", x))
                .ThenBy(x => x)
                .First();
            return ("director", director, director);
        }

        var repeatedCountry = RecommendationViewHelper.SplitCsv(movie.Country)
            .OrderByDescending(x => GetExistingMismatchMarks(preferences, "country", x))
            .FirstOrDefault(x => GetExistingMismatchMarks(preferences, "country", x) > 0);
        if (!string.IsNullOrWhiteSpace(repeatedCountry))
        {
            return ("country", repeatedCountry, repeatedCountry);
        }

        var repeatedLanguage = RecommendationViewHelper.SplitCsv(movie.Language)
            .OrderByDescending(x => GetExistingMismatchMarks(preferences, "language", x))
            .FirstOrDefault(x => GetExistingMismatchMarks(preferences, "language", x) > 0);
        if (!string.IsNullOrWhiteSpace(repeatedLanguage))
        {
            return ("language", repeatedLanguage, repeatedLanguage);
        }

        return null;
    }

    private static int GetExistingMismatchMarks(AppUserPreferences preferences, string kind, string value)
    {
        var state = preferences.MismatchFactors.FirstOrDefault(x => string.Equals(x.Key, BuildMismatchKey(kind, value), StringComparison.OrdinalIgnoreCase));
        return state is null ? 0 : state.PositiveMarks + state.NegativeMarks;
    }

    private static string BuildMismatchKey(string kind, string value)
        => $"{kind.Trim().ToLowerInvariant()}:{value.Trim().ToLowerInvariant()}";

    private static MismatchFactorState GetOrCreateMismatchFactor(AppUserPreferences preferences, string kind, string value, string label)
    {
        var key = BuildMismatchKey(kind, value);
        var state = preferences.MismatchFactors.FirstOrDefault(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase));
        if (state is not null)
        {
            state.Kind = kind;
            state.Value = value;
            state.Label = string.IsNullOrWhiteSpace(label) ? value : label;
            return state;
        }

        state = new MismatchFactorState
        {
            Key = key,
            Kind = kind,
            Value = value,
            Label = string.IsNullOrWhiteSpace(label) ? value : label
        };
        preferences.MismatchFactors.Add(state);
        return state;
    }

    private static void ResetMismatchMarks(MismatchFactorState state)
    {
        state.PositiveMarks = 0;
        state.NegativeMarks = 0;
    }

    private static bool ApplyPreferenceAdjustment(AppUserPreferences preferences, string kind, string value, decimal delta)
    {
        var map = kind.Trim().ToLowerInvariant() switch
        {
            "genre" => preferences.GenreAdjustments,
            "director" => preferences.DirectorAdjustments,
            "country" => preferences.CountryAdjustments,
            "language" => preferences.LanguageAdjustments,
            _ => null
        };

        if (map is null)
        {
            return false;
        }

        var existing = map.TryGetValue(value, out var current) ? current : 0m;
        var updated = Math.Clamp(existing + delta, -3m, 3m);
        if (updated == 0m)
        {
            map.Remove(value);
        }
        else
        {
            map[value] = updated;
        }

        return true;
    }

    private IActionResult RedirectToLocalOr(string? returnUrl, string fallbackAction)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url is not null && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction(fallbackAction);
    }

    private IActionResult RedirectToDeleteReturnUrl(string? returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl)
            && Url is not null
            && Url.IsLocalUrl(returnUrl)
            && !IsMovieDetailsUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction(nameof(Index));
    }

    private IActionResult AjaxOrRedirect(
        bool success,
        bool? watched,
        decimal? userRating,
        PersonalVerdict? primaryVerdict,
        string[]? reasonTags,
        string? returnUrl,
        decimal? personalMatchScore = null,
        decimal? predictedScore = null,
        string? predictedLabel = null,
        string? predictedReason = null)
    {
        if (IsAjaxRequest())
        {
            return Json(new
            {
                success,
                watched,
                userRating,
                primaryVerdict = primaryVerdict?.ToString(),
                primaryVerdictLabel = RecommendationViewHelper.GetVerdictLabel(primaryVerdict),
                reasonTags = reasonTags ?? [],
                personalMatchScore = personalMatchScore?.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture),
                predictedScore = predictedScore?.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture),
                predictedLabel,
                predictedReason
            });
        }

        return RedirectToLocalOr(returnUrl, nameof(Index));
    }

    private IActionResult WowAjaxOrRedirect(bool success, string message, bool isWowPick, int wowCount, int wowLimit, string? returnUrl)
    {
        if (IsAjaxRequest())
        {
            return Json(new
            {
                success,
                message,
                isWowPick,
                wowCount,
                wowLimit
            });
        }

        TempData["StatusMessage"] = message;
        return RedirectToLocalOr(returnUrl, nameof(Index));
    }

    private bool IsAjaxRequest()
        => string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);

    private static bool ShouldRedirectToDetails(string? returnUrl)
        => !string.IsNullOrWhiteSpace(returnUrl)
           && returnUrl.Contains("section=review", StringComparison.OrdinalIgnoreCase);

    private static bool IsMovieDetailsUrl(string returnUrl)
        => returnUrl.Contains("/Movies/Details", StringComparison.OrdinalIgnoreCase)
           || returnUrl.Contains("/Movies/Details/", StringComparison.OrdinalIgnoreCase)
           || returnUrl.Contains("action=Details", StringComparison.OrdinalIgnoreCase);

    private static void NormalizeManualReviewFields(MovieEditViewModel model, bool preserveWatchedSelectionForReview = false)
    {
        if (model.WatchedStatus == WatchedStatus.Watched)
        {
            if (model.PrimaryVerdict.HasValue)
            {
                model.UserGrade = RecommendationViewHelper.MapVerdictToGrade(model.PrimaryVerdict.Value);
                model.UserRating = RecommendationViewHelper.MapVerdictToUserRating(model.PrimaryVerdict.Value);
                return;
            }

            if (model.UserGrade.HasValue)
            {
                model.PrimaryVerdict = RecommendationViewHelper.MapGradeToVerdict(model.UserGrade);
                model.UserRating = RecommendationViewHelper.MapGradeToUserRating(model.UserGrade.Value);
                return;
            }

            if (preserveWatchedSelectionForReview)
            {
                model.WatchedStatus = WatchedStatus.NotWatched;
                model.NeedsTagReview = false;
                model.UserRating = null;
                model.UserGrade = null;
                model.PrimaryVerdict = null;
                model.ReasonTagsCsv = null;
                model.SelectedReasonTags = [];
                return;
            }

            model.NeedsTagReview = true;
            model.UserRating = null;
            return;
        }

        model.UserRating = null;
        model.UserGrade = null;
        model.PrimaryVerdict = null;
        model.ReasonTagsCsv = null;
        model.SelectedReasonTags = [];
        model.NeedsTagReview = false;
    }

    private async Task<AddMoviePageViewModel> BuildAddPageModelAsync(MovieEditViewModel model, string? lookupTitle, int? lookupYear, string? selectedImdbId, CancellationToken cancellationToken)
    {
        return new AddMoviePageViewModel
        {
            LookupTitle = lookupTitle ?? model.Title,
            LookupYear = lookupYear ?? model.Year,
            ShowSavePopup = true,
            Movie = model,
            SelectedImdbId = selectedImdbId,
            Candidates = (await _metadataService.SearchCandidatesAsync(lookupTitle ?? model.Title, lookupYear ?? model.Year, 10, cancellationToken)).ToList()
        };
    }

    private void ApplyAddReviewState(MovieEditViewModel model, PersonalVerdict? addVerdict, List<string>? addReasonTags, bool queueForReview)
    {
        if (model.WatchedStatus != WatchedStatus.Watched)
        {
            NormalizeManualReviewFields(model);
            return;
        }

        if (addVerdict.HasValue)
        {
            model.PrimaryVerdict = addVerdict.Value;
            model.UserGrade = RecommendationViewHelper.MapVerdictToGrade(addVerdict.Value);
            model.UserRating = RecommendationViewHelper.MapVerdictToUserRating(addVerdict.Value);
            model.SelectedReasonTags = (addReasonTags ?? [])
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Take(RecommendationViewHelper.MaxReasonTags)
                .ToList();
            model.ReasonTagsCsv = RecommendationViewHelper.JoinCsv(model.SelectedReasonTags);
            model.NeedsTagReview = model.SelectedReasonTags.Count < RecommendationViewHelper.GetMinimumReasonTags(model.UserGrade);
            return;
        }

        if (queueForReview)
        {
            model.UserRating = null;
            model.UserGrade = null;
            model.PrimaryVerdict = null;
            model.ReasonTagsCsv = null;
            model.SelectedReasonTags = [];
            model.NeedsTagReview = true;
            return;
        }

        NormalizeManualReviewFields(model);
    }

    private async Task<MovieDetailsViewModel?> BuildEstimatePreviewAsync(MovieEditViewModel model, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(model.Title))
        {
            return null;
        }

        var movie = model.ToEntity();
        movie.Id = -1;
        movie.NormalizedTitle = TitleNormalizer.Normalize(movie.Title);
        movie.WatchedStatus = WatchedStatus.NotWatched;
        movie.UserRating = null;
        movie.UserGrade = null;
        movie.PrimaryVerdict = null;
        movie.ReasonTagsCsv = null;
        movie.NormalizedTagsCsv = null;
        movie.NeedsTagReview = false;
        movie.IsDismissed = false;

        var movies = await _dbContext.Movies.AsNoTracking().ToListAsync(cancellationToken);
        movies.Add(movie);

        var preferences = _preferencesService.Get();
        var results = _recommendationEngine.CalculateResults(movies, preferences);
        if (!results.TryGetValue(movie.Id, out var result))
        {
            return null;
        }

        movie.PersonalMatchScore = result.PersonalMatchScore;
        movie.PredictedScore = result.FinalScore;
        movie.PredictedLabel = result.PredictedLabel;
        movie.PredictedReason = result.PredictedReason;
        movie.RecommendationContextJson = JsonSerializer.Serialize(result.Context);
        movie.PlotKeywordsCsv = RecommendationCatalog.JoinCsv(result.Context.PlotKeywords);

        var referenceMovie = await ResolveReferenceMovieAsync(movie, cancellationToken);
        var wowCount = await _dbContext.Movies.CountAsync(x => x.IsWowPick, cancellationToken);
        var wowLimit = WowPickHelper.GetWowLimit(await _dbContext.Movies.CountAsync(cancellationToken), preferences);
        return MovieDetailsViewModel.Create(movie, preferences.DismissScoreThreshold, wowCount, wowLimit, referenceMovie);
    }

    private static MovieEditViewModel MapLookupResult(MetadataLookupResult result)
    {
        return new MovieEditViewModel
        {
            Title = result.Title,
            OriginalTitle = result.OriginalTitle,
            Year = result.Year,
            Category = result.Category,
            GenresCsv = result.GenresCsv,
            MediaType = result.MediaType,
            ImdbRating = result.ImdbRating,
            ImdbVotes = result.ImdbVotes,
            Metascore = result.Metascore,
            RuntimeMinutes = result.RuntimeMinutes,
            ReleasedOn = result.ReleasedOn,
            Country = result.Country,
            Language = result.Language,
            Director = result.Director,
            Writer = result.Writer,
            Actors = result.Actors,
            PosterUrl = result.PosterUrl,
            Overview = result.Overview,
            OmdbType = result.OmdbType,
            OmdbRatingsJson = result.OmdbRatingsJson,
            TmdbId = result.TmdbId,
            TmdbKeywordsCsv = result.TmdbKeywordsCsv,
            SimilarTitlesJson = result.SimilarTitlesJson,
            ExternalRatingsJson = result.ExternalRatingsJson,
            ExternalId = result.ExternalId,
            ExternalSource = result.ExternalSource,
            WatchedStatus = WatchedStatus.NotWatched
        };
    }

    private static decimal GetRecommendationSortScore(Movie movie)
        => RecommendationViewHelper.GetDisplayMatchScore(movie);

    private sealed record MismatchSuggestionPayload(
        string Kind,
        string Value,
        string Label,
        string Direction,
        string Prompt,
        int Marks,
        decimal MismatchValue);
}
