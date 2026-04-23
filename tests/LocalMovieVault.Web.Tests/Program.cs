using LocalMovieVault.Web.Contracts;
using LocalMovieVault.Web.Data;
using LocalMovieVault.Web.Helpers;
using LocalMovieVault.Web.Models;
using LocalMovieVault.Web.Services;
using LocalMovieVault.Web.Services.Recommendations;
using LocalMovieVault.Web.Controllers;
using LocalMovieVault.Web.ViewModels;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System.Text.Json;
using System.Text.Json.Nodes;

if (args.Length >= 2 && string.Equals(args[0], "--apply-csv", StringComparison.OrdinalIgnoreCase))
{
    await ApplyCsvDeltaAsync(args[1]);
    return;
}

if (args.Length >= 1 && string.Equals(args[0], "--db-count", StringComparison.OrdinalIgnoreCase))
{
    await PrintDatabaseCountAsync();
    return;
}

if (args.Length >= 2 && string.Equals(args[0], "--restore-export", StringComparison.OrdinalIgnoreCase))
{
    await RestoreFromExportAsync(args[1]);
    return;
}

if (args.Length >= 1 && string.Equals(args[0], "--find-duplicates", StringComparison.OrdinalIgnoreCase))
{
    await FindDuplicatesAsync();
    return;
}

RunSmokeStep("AssertCanonicalTagAlias", () => AssertCanonicalTagAlias("Beautiful visuals", "Incredible visuals"));
RunSmokeStep("AssertLegacyMigrationDropsAmbiguousTags", AssertLegacyMigrationDropsAmbiguousTags);
RunSmokeStep("AssertGradeMapping", AssertGradeMapping);
RunSmokeStep("AssertDisplayMatchScorePrefersPredictedForUnwatched", AssertDisplayMatchScorePrefersPredictedForUnwatched);
RunSmokeStep("AssertDisplayMatchScorePrefersPredictedForWatched", AssertDisplayMatchScorePrefersPredictedForWatched);
RunSmokeStep("AssertReviewBadgeUsesPredictedScoreForUnwatched", AssertReviewBadgeUsesPredictedScoreForUnwatched);
RunSmokeStep("AssertUnwatchedReviewQueueFlagTriggersReviewState", AssertUnwatchedReviewQueueFlagTriggersReviewState);
RunSmokeStep("AssertGenreAwareTagGrouping", AssertGenreAwareTagGrouping);
RunSmokeStep("AssertBestMatchUsesGenreDropdown", AssertBestMatchUsesGenreDropdown);
RunSmokeStep("AssertDiscoveryCardUsesStatusToggleLayout", AssertDiscoveryCardUsesStatusToggleLayout);
RunSmokeStep("AssertDiscoveryCardShowsIndependentMetricLabels", AssertDiscoveryCardShowsIndependentMetricLabels);
RunSmokeStep("AssertDiscoveryCardShowsUserMetricOnlyAfterCompletedReview", AssertDiscoveryCardShowsUserMetricOnlyAfterCompletedReview);
RunSmokeStep("AssertMovieDetailsViewUsesUpdatedDetailsLayout", AssertMovieDetailsViewUsesUpdatedDetailsLayout);
RunSmokeStep("AssertMovieDetailsViewUsesBottomEditAndDeleteActions", AssertMovieDetailsViewUsesBottomEditAndDeleteActions);
RunSmokeStep("AssertMovieDetailsViewModelUsesHeroSlotRules", AssertMovieDetailsViewModelUsesHeroSlotRules);
await RunSmokeStepAsync("AssertMovieDetailsDetailsActionLoadsReferenceMovieAsync", AssertMovieDetailsDetailsActionLoadsReferenceMovieAsync);
RunSmokeStep("AssertWatchModalSupportsReviewLater", AssertWatchModalSupportsReviewLater);
RunSmokeStep("AssertGenreNormalizerSupportsPipeSeparatedGenres", AssertGenreNormalizerSupportsPipeSeparatedGenres);
RunSmokeStep("AssertDiscoveryCardDoesNotShowPredictionNarrative", AssertDiscoveryCardDoesNotShowPredictionNarrative);
RunSmokeStep("AssertToggleUnwatchedFallsBackToFormPost", AssertToggleUnwatchedFallsBackToFormPost);
RunSmokeStep("AssertToggleUnwatchedUsesDedicatedToggleForm", AssertToggleUnwatchedUsesDedicatedToggleForm);
RunSmokeStep("AssertImportantTagsMigrateToCanonicalArray", AssertImportantTagsMigrateToCanonicalArray);
await RunSmokeStepAsync("AssertCsvDeltaParserAsync", AssertCsvDeltaParserAsync);
await RunSmokeStepAsync("AssertRecommendationUsesNormalizedTagsAsync", AssertRecommendationUsesNormalizedTagsAsync);
await RunSmokeStepAsync("AssertSingleWatchedMovieDoesNotSelfBoostAsync", AssertSingleWatchedMovieDoesNotSelfBoostAsync);
await RunSmokeStepAsync("AssertMehRatingDoesNotPenalizeMatchingCandidateAsync", AssertMehRatingDoesNotPenalizeMatchingCandidateAsync);
await RunSmokeStepAsync("AssertStrongCandidateCanScoreHighWithoutPerfectTagOverlapAsync", AssertStrongCandidateCanScoreHighWithoutPerfectTagOverlapAsync);
await RunSmokeStepAsync("AssertLowScoreExplanationDoesNotSoundPositiveAsync", AssertLowScoreExplanationDoesNotSoundPositiveAsync);
await RunSmokeStepAsync("AssertNeedsTagReviewMovieDoesNotTrainRecommendationsAsync", AssertNeedsTagReviewMovieDoesNotTrainRecommendationsAsync);
await RunSmokeStepAsync("AssertScoreCalibrationDoesNotOveruseTopBandAsync", AssertScoreCalibrationDoesNotOveruseTopBandAsync);
await RunSmokeStepAsync("AssertImportantTagsBoostMatchingCandidateMoreThanGenericAsync", AssertImportantTagsBoostMatchingCandidateMoreThanGenericAsync);
await RunSmokeStepAsync("AssertSettingsPreviewUsesTemporaryImportantTagsWithoutPersistingAsync", AssertSettingsPreviewUsesTemporaryImportantTagsWithoutPersistingAsync);
await RunSmokeStepAsync("AssertSettingsPreviewFallsBackWhenPinnedMovieDoesNotChangeAsync", AssertSettingsPreviewFallsBackWhenPinnedMovieDoesNotChangeAsync);
await RunSmokeStepAsync("AssertSettingsSavePersistsImportantTagsAndRecalculatesAsync", AssertSettingsSavePersistsImportantTagsAndRecalculatesAsync);
await RunSmokeStepAsync("AssertSettingsSaveUsesSelectionsWhenPreferencesAreNotPostedAsync", AssertSettingsSaveUsesSelectionsWhenPreferencesAreNotPostedAsync);
await RunSmokeStepAsync("AssertToggleWatchedClearsLegacyScoreAsync", AssertToggleWatchedClearsLegacyScoreAsync);
await RunSmokeStepAsync("AssertCompleteWatchRequiresThreeTagsAsync", AssertCompleteWatchRequiresThreeTagsAsync);
await RunSmokeStepAsync("AssertCompleteWatchWritesAppEventAsync", AssertCompleteWatchWritesAppEventAsync);
await RunSmokeStepAsync("AssertCompleteWatchShowsImmediateMismatchSuggestionAsync", AssertCompleteWatchShowsImmediateMismatchSuggestionAsync);
await RunSmokeStepAsync("AssertCompleteWatchAccumulatesMismatchMarksAsync", AssertCompleteWatchAccumulatesMismatchMarksAsync);
await RunSmokeStepAsync("AssertDismissedMismatchSuggestionAppliesCooldownAsync", AssertDismissedMismatchSuggestionAppliesCooldownAsync);
await RunSmokeStepAsync("AssertAcceptingMismatchSuggestionPersistsPreferenceAsync", AssertAcceptingMismatchSuggestionPersistsPreferenceAsync);
await RunSmokeStepAsync("AssertDismissAndRestoreWriteAppEventsAsync", AssertDismissAndRestoreWriteAppEventsAsync);
await RunSmokeStepAsync("AssertImportUploadWritesAppEventAsync", AssertImportUploadWritesAppEventAsync);
await RunSmokeStepAsync("AssertRestoreFromExportCsvAsync", AssertRestoreFromExportCsvAsync);
RunSmokeStep("AssertSettingsViewUsesTastePrioritiesSection", AssertSettingsViewUsesTastePrioritiesSection);
RunSmokeStep("AssertSettingsViewDoesNotExposeDeferredSliders", AssertSettingsViewDoesNotExposeDeferredSliders);
RunSmokeStep("AssertSettingsViewIncludesRandomizeButtonHook", AssertSettingsViewIncludesRandomizeButtonHook);
RunSmokeStep("AssertSettingsViewIncludesDiagnosticsLink", AssertSettingsViewIncludesDiagnosticsLink);
RunSmokeStep("AssertAddLookupFormIncludesAntiForgeryToken", AssertAddLookupFormIncludesAntiForgeryToken);
RunSmokeStep("AssertSizeLimitedFileLogRollsOver", AssertSizeLimitedFileLogRollsOver);

Console.WriteLine("All LocalMovieVault.Web smoke tests passed.");
SmokeTrace("PASS All LocalMovieVault.Web smoke tests passed.");

static void RunSmokeStep(string name, Action action)
{
    Console.WriteLine($"RUN {name}");
    SmokeTrace($"RUN {name}");
    action();
    Console.WriteLine($"PASS {name}");
    SmokeTrace($"PASS {name}");
}

static async Task RunSmokeStepAsync(string name, Func<Task> action)
{
    Console.WriteLine($"RUN {name}");
    SmokeTrace($"RUN {name}");
    await action();
    Console.WriteLine($"PASS {name}");
    SmokeTrace($"PASS {name}");
}

static void SmokeTrace(string message)
{
    var path = Path.Combine(FindProjectRoot(), "logs", "smoke-progress.log");
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    File.AppendAllText(path, $"[{DateTime.UtcNow:O}] {message}{Environment.NewLine}");
}

static async Task ApplyCsvDeltaAsync(string csvPath)
{
    if (!File.Exists(csvPath))
    {
        throw new FileNotFoundException("CSV delta file not found.", csvPath);
    }

    var projectRoot = FindProjectRoot();
    var contentRoot = Path.Combine(projectRoot, "src", "LocalMovieVault.Web");
    var configuration = new ConfigurationBuilder()
        .AddJsonFile(Path.Combine(contentRoot, "appsettings.json"), optional: false)
        .Build();

    var storage = AppStorageBootstrapper.Initialize(contentRoot, configuration);
    var options = new DbContextOptionsBuilder<AppDbContext>()
        .UseSqlite($"Data Source={storage.DatabasePath}")
        .Options;

    await using var dbContext = new AppDbContext(options);
    var migrator = new DatabaseSchemaMigrator();
    await migrator.MigrateAsync(dbContext);

    var parser = new CsvMovieDeltaImportService();
    await using var stream = File.OpenRead(csvPath);
    var rows = await parser.ParseAsync(stream);
    var upsert = new MovieUpsertService(dbContext);

    foreach (var row in rows)
    {
        await upsert.ApplyCsvDeltaAsync(row);
    }

    var settingsService = new AppUserPreferencesService(storage);
    var engine = new DeterministicRecommendationEngine(
        dbContext,
        new RecommendationFeatureExtractor(new PlotKeywordExtractor()),
        new RecommendationExplainer(),
        settingsService);
    await engine.RecalculateAsync();

    Console.WriteLine($"Applied CSV delta rows: {rows.Count}");
    Console.WriteLine($"Database: {storage.DatabasePath}");
}

static string FindProjectRoot()
{
    var current = new DirectoryInfo(AppContext.BaseDirectory);
    while (current is not null)
    {
        if (File.Exists(Path.Combine(current.FullName, "AGENTS.md")))
        {
            return current.FullName;
        }

        current = current.Parent;
    }

    throw new DirectoryNotFoundException("Could not locate the MovieDb project root.");
}

static async Task PrintDatabaseCountAsync()
{
    var projectRoot = FindProjectRoot();
    var contentRoot = Path.Combine(projectRoot, "src", "LocalMovieVault.Web");
    var configuration = new ConfigurationBuilder()
        .AddJsonFile(Path.Combine(contentRoot, "appsettings.json"), optional: false)
        .Build();

    var storage = AppStorageBootstrapper.Initialize(contentRoot, configuration);
    var options = new DbContextOptionsBuilder<AppDbContext>()
        .UseSqlite($"Data Source={storage.DatabasePath}")
        .Options;

    await using var dbContext = new AppDbContext(options);
    var count = await dbContext.Movies.CountAsync();
    Console.WriteLine($"Database: {storage.DatabasePath}");
    Console.WriteLine($"MovieCount: {count}");
    Console.WriteLine($"WatchedCount: {await dbContext.Movies.CountAsync(x => x.WatchedStatus == WatchedStatus.Watched)}");
    Console.WriteLine($"NotWatchedCount: {await dbContext.Movies.CountAsync(x => x.WatchedStatus == WatchedStatus.NotWatched)}");
    Console.WriteLine($"UnknownCount: {await dbContext.Movies.CountAsync(x => x.WatchedStatus == WatchedStatus.Unknown)}");
    Console.WriteLine($"NeedsTagReviewCount: {await dbContext.Movies.CountAsync(x => x.NeedsTagReview)}");

    var sample = await dbContext.Movies
        .OrderBy(x => x.Id)
        .Take(5)
        .Select(x => new { x.Id, x.Title, x.Year, x.UserGrade, x.WatchedStatus })
        .ToListAsync();

    foreach (var item in sample)
    {
        Console.WriteLine($"{item.Id}: {item.Title} | {item.Year} | {item.UserGrade} | {item.WatchedStatus}");
    }
}

static async Task RestoreFromExportAsync(string csvPath)
{
    if (!File.Exists(csvPath))
    {
        throw new FileNotFoundException("Export CSV file not found.", csvPath);
    }

    var projectRoot = FindProjectRoot();
    var contentRoot = Path.Combine(projectRoot, "src", "LocalMovieVault.Web");
    var configuration = new ConfigurationBuilder()
        .AddJsonFile(Path.Combine(contentRoot, "appsettings.json"), optional: false)
        .Build();

    var storage = AppStorageBootstrapper.Initialize(contentRoot, configuration);
    var dbPath = storage.DatabasePath;
    var backupPath = dbPath + ".pre-restore-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + ".bak";
    File.Copy(dbPath, backupPath, overwrite: false);

    var options = new DbContextOptionsBuilder<AppDbContext>()
        .UseSqlite($"Data Source={dbPath}")
        .Options;

    await using var dbContext = new AppDbContext(options);
    var restore = new ExportCsvRestoreService();
    await using var stream = File.OpenRead(csvPath);
    var rows = await restore.ParseExportAsync(stream);
    foreach (var row in rows)
    {
        await restore.ApplyAsync(dbContext, row);
    }

    var settingsService = new AppUserPreferencesService(storage);
    var engine = new DeterministicRecommendationEngine(
        dbContext,
        new RecommendationFeatureExtractor(new PlotKeywordExtractor()),
        new RecommendationExplainer(),
        settingsService);
    await engine.RecalculateAsync();

    Console.WriteLine($"Restore rows processed: {rows.Count}");
    Console.WriteLine($"Database: {dbPath}");
    Console.WriteLine($"Backup: {backupPath}");
}

static async Task FindDuplicatesAsync()
{
    var projectRoot = FindProjectRoot();
    var contentRoot = Path.Combine(projectRoot, "src", "LocalMovieVault.Web");
    var configuration = new ConfigurationBuilder()
        .AddJsonFile(Path.Combine(contentRoot, "appsettings.json"), optional: false)
        .Build();

    var storage = AppStorageBootstrapper.Initialize(contentRoot, configuration);
    var options = new DbContextOptionsBuilder<AppDbContext>()
        .UseSqlite($"Data Source={storage.DatabasePath}")
        .Options;

    await using var dbContext = new AppDbContext(options);
    var duplicates = await dbContext.Movies
        .AsNoTracking()
        .GroupBy(x => new { x.NormalizedTitle, x.Year, x.MediaType })
        .Where(x => x.Count() > 1)
        .Select(x => new
        {
            x.Key.NormalizedTitle,
            x.Key.Year,
            x.Key.MediaType,
            Count = x.Count(),
            Titles = x.OrderBy(m => m.Id).Select(m => m.Title + " #" + m.Id)
        })
        .ToListAsync();

    Console.WriteLine($"DuplicateGroups: {duplicates.Count}");
    foreach (var duplicate in duplicates)
    {
        Console.WriteLine($"{duplicate.NormalizedTitle} | {duplicate.Year} | {duplicate.MediaType} | {duplicate.Count}");
        foreach (var title in duplicate.Titles)
        {
            Console.WriteLine("  " + title);
        }
    }
}

static void AssertCanonicalTagAlias(string input, string expected)
{
    var actual = RecommendationViewHelper.CanonicalizeReasonTag(input);
    if (!string.Equals(actual, expected, StringComparison.Ordinal))
    {
        throw new Exception($"Expected alias '{input}' to map to '{expected}', but found '{actual}'.");
    }
}

static void AssertLegacyMigrationDropsAmbiguousTags()
{
    var migrated = RecommendationViewHelper.MigrateLegacyTags("Atmosphere, Great acting, Twist, Original idea");
    if (!migrated.Contains("Great acting", StringComparer.OrdinalIgnoreCase) || !migrated.Contains("Original idea", StringComparer.OrdinalIgnoreCase))
    {
        throw new Exception("Expected deterministic legacy tags to survive migration.");
    }

    if (migrated.Contains("Atmosphere", StringComparer.OrdinalIgnoreCase) || migrated.Contains("Twist", StringComparer.OrdinalIgnoreCase))
    {
        throw new Exception("Expected ambiguous legacy tags to be dropped during migration.");
    }
}

static void AssertGradeMapping()
{
    if (RecommendationViewHelper.MapScoreToGrade(95m) != UserGrade.Loved) throw new Exception("95 should map to Loved.");
    if (RecommendationViewHelper.MapScoreToGrade(80m) != UserGrade.Liked) throw new Exception("80 should map to Liked.");
    if (RecommendationViewHelper.MapScoreToGrade(55m) != UserGrade.Meh) throw new Exception("55 should map to Meh.");
    if (RecommendationViewHelper.MapScoreToGrade(20m) != UserGrade.CouldntFinish) throw new Exception("20 should map to Couldn't finish.");
}

static void AssertSizeLimitedFileLogRollsOver()
{
    var tempRoot = Path.Combine(Path.GetTempPath(), "LocalMovieVault.Tests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(tempRoot);

    try
    {
        var activePath = Path.Combine(tempRoot, "app.log");
        var archivePath = Path.Combine(tempRoot, "app.log.previous");
        var writer = new SizeLimitedFileLogWriter(activePath, 128);

        writer.WriteLine("first-entry-" + new string('a', 90));
        writer.WriteLine("second-entry-" + new string('b', 90));

        if (!File.Exists(activePath))
        {
            throw new Exception("Expected active log file to exist.");
        }

        if (!File.Exists(archivePath))
        {
            throw new Exception("Expected rollover to create the previous log archive.");
        }

        var activeText = File.ReadAllText(activePath);
        var archiveText = File.ReadAllText(archivePath);

        if (!archiveText.Contains("first-entry-", StringComparison.Ordinal))
        {
            throw new Exception("Expected the first log entry to move into the archive.");
        }

        if (!activeText.Contains("second-entry-", StringComparison.Ordinal))
        {
            throw new Exception("Expected the latest log entry to remain in the active file.");
        }

        if (new FileInfo(activePath).Length > 128)
        {
            throw new Exception("Expected the active log file to stay within the configured size limit after rollover.");
        }
    }
    finally
    {
        if (Directory.Exists(tempRoot))
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }
}

static void AssertDisplayMatchScorePrefersPredictedForUnwatched()
{
    var movie = new Movie
    {
        Title = "Display Score",
        MediaType = MediaType.Movie,
        WatchedStatus = WatchedStatus.NotWatched,
        PersonalMatchScore = 100m,
        PredictedScore = 54.2m
    };

    var score = RecommendationViewHelper.GetDisplayMatchScore(movie);
    if (score != 54.2m)
    {
        throw new Exception($"Expected unwatched display score to prefer PredictedScore, found {score}.");
    }
}

static void AssertDisplayMatchScorePrefersPredictedForWatched()
{
    var movie = new Movie
    {
        Title = "Watched Display Score",
        MediaType = MediaType.Movie,
        WatchedStatus = WatchedStatus.Watched,
        UserRating = 95m,
        UserGrade = UserGrade.Loved,
        PersonalMatchScore = 64m,
        PredictedScore = 71.3m
    };

    var score = RecommendationViewHelper.GetDisplayMatchScore(movie);
    if (score != 71.3m)
    {
        throw new Exception($"Expected watched display score to keep preferring PredictedScore, found {score}.");
    }
}

static void AssertReviewBadgeUsesPredictedScoreForUnwatched()
{
    var movie = new Movie
    {
        Title = "Dismiss Candidate",
        MediaType = MediaType.Movie,
        WatchedStatus = WatchedStatus.NotWatched,
        PersonalMatchScore = 100m,
        PredictedScore = 42m
    };

    var badge = MovieStateHelper.GetReviewBadge(movie, 50m);
    if (!string.Equals(badge, "Suggested dismiss", StringComparison.Ordinal))
    {
        throw new Exception($"Expected unwatched review badge to use predicted score threshold, found '{badge ?? "<null>"}'.");
    }
}

static void AssertUnwatchedReviewQueueFlagTriggersReviewState()
{
    var movie = new Movie
    {
        Title = "Review Queue Candidate",
        MediaType = MediaType.Movie,
        WatchedStatus = WatchedStatus.NotWatched,
        NeedsTagReview = true,
        PredictedScore = 88m
    };

    if (!MovieStateHelper.NeedsReview(movie, 50m))
    {
        throw new Exception("Expected unwatched review-queue titles to be treated as review items.");
    }
}

static void AssertGenreAwareTagGrouping()
{
    var general = RecommendationViewHelper.GetGeneralReasonTagDefinitions();
    var genreSpecific = RecommendationViewHelper.GetGenreSpecificReasonTagDefinitions("Horror, Thriller");

    if (!general.Any(x => x.Label == "Original idea"))
    {
        throw new Exception("Expected general tags to include 'Original idea'.");
    }

    if (general.Any(x => x.Label == "Scary"))
    {
        throw new Exception("Expected general tags to exclude genre-specific horror tags.");
    }

    if (!genreSpecific.Any(x => x.Label == "Scary"))
    {
        throw new Exception("Expected horror genre tags to include 'Scary'.");
    }

    if (genreSpecific.Any(x => x.Label == "Funny"))
    {
        throw new Exception("Expected horror genre tags to exclude comedy-only tags.");
    }
}

static void AssertBestMatchUsesGenreDropdown()
{
    var path = Path.Combine(FindProjectRoot(), "src", "LocalMovieVault.Web", "Views", "Home", "BestMatch.cshtml");
    var content = File.ReadAllText(path);

    if (!content.Contains("bestMatchGenre", StringComparison.Ordinal) || !content.Contains("<select", StringComparison.Ordinal))
    {
        throw new Exception("Expected Best Match to render the genre picker as a dropdown.");
    }
}

static void AssertDiscoveryCardUsesStatusToggleLayout()
{
    var path = Path.Combine(FindProjectRoot(), "src", "LocalMovieVault.Web", "Views", "Shared", "_DiscoveryCard.cshtml");
    var content = File.ReadAllText(path);

    var requiredMarkers = new[]
    {
        "movie-card-status-toggle",
        "movie-card-title-frame",
        "movie-card-meta-line",
        "movie-card-score-line"
    };

    foreach (var marker in requiredMarkers)
    {
        if (!content.Contains(marker, StringComparison.Ordinal))
        {
            throw new Exception($"Expected discovery card markup to contain '{marker}'.");
        }
    }
}

static void AssertDiscoveryCardShowsIndependentMetricLabels()
{
    var path = Path.Combine(FindProjectRoot(), "src", "LocalMovieVault.Web", "Views", "Shared", "_DiscoveryCard.cshtml");
    var content = File.ReadAllText(path);

    var requiredMarkers = new[]
    {
        "IMDb",
        "Match",
        "User"
    };

    foreach (var marker in requiredMarkers)
    {
        if (!content.Contains(marker, StringComparison.Ordinal))
        {
            throw new Exception($"Expected discovery card metric row to contain '{marker}'.");
        }
    }
}

static void AssertDiscoveryCardShowsUserMetricOnlyAfterCompletedReview()
{
    var path = Path.Combine(FindProjectRoot(), "src", "LocalMovieVault.Web", "Views", "Shared", "_DiscoveryCard.cshtml");
    var content = File.ReadAllText(path);

    if (!content.Contains("!Model.NeedsTagReview", StringComparison.Ordinal) ||
        !content.Contains("Model.UserRating", StringComparison.Ordinal))
    {
        throw new Exception("Expected discovery card to render the User metric only after review is complete.");
    }
}

static void AssertMovieDetailsViewUsesUpdatedDetailsLayout()
{
    var path = Path.Combine(FindProjectRoot(), "src", "LocalMovieVault.Web", "Views", "Movies", "MovieDetails.cshtml");
    var content = File.ReadAllText(path);

    if (!content.Contains("Short summary", StringComparison.Ordinal) ||
        !content.Contains("Rating explanation", StringComparison.Ordinal) ||
        !content.Contains("Fit", StringComparison.Ordinal) ||
        !content.Contains("Comparison vs", StringComparison.Ordinal) ||
        !content.Contains("details-compare-stack", StringComparison.Ordinal))
    {
        throw new Exception("Expected movie details view to use the updated summary, fit metric, and responsive comparison layout.");
    }

    if (content.Contains("Chosen by the app for comparison", StringComparison.Ordinal) ||
        content.Contains("One named reference movie, shown once for clarity", StringComparison.Ordinal))
    {
        throw new Exception("Expected movie details comparison header to remove the extra helper copy.");
    }
}

static void AssertMovieDetailsViewUsesBottomEditAndDeleteActions()
{
    var path = Path.Combine(FindProjectRoot(), "src", "LocalMovieVault.Web", "Views", "Movies", "MovieDetails.cshtml");
    var content = File.ReadAllText(path);

    if (!content.Contains("Edit review", StringComparison.Ordinal) ||
        !content.Contains("asp-action=\"Delete\"", StringComparison.Ordinal) ||
        !content.Contains("js-open-watch-modal", StringComparison.Ordinal))
    {
        throw new Exception("Expected movie details view to expose the bottom edit-review and delete actions.");
    }
}

static void AssertMovieDetailsViewModelUsesHeroSlotRules()
{
    var completeMovie = new Movie
    {
        Title = "Complete",
        MediaType = MediaType.Movie,
        WatchedStatus = WatchedStatus.Watched,
        UserRating = 95m,
        UserGrade = UserGrade.Loved,
        PrimaryVerdict = PersonalVerdict.Loved,
        NormalizedTagsCsv = "Great acting, Original idea, Incredible visuals",
        PredictedScore = 88m
    };
    var completeModel = MovieDetailsViewModel.Create(completeMovie, 50m);
    if (completeModel.ShowHeroReviewAction)
    {
        throw new Exception("Expected completed watched movies to show the rating slot instead of the hero review action.");
    }

    var reviewMovie = new Movie
    {
        Title = "Under Review",
        MediaType = MediaType.Movie,
        WatchedStatus = WatchedStatus.Watched,
        UserRating = 55m,
        UserGrade = UserGrade.Meh,
        PrimaryVerdict = PersonalVerdict.Okay,
        NeedsTagReview = true,
        PredictedScore = 26.8m
    };
    var reviewModel = MovieDetailsViewModel.Create(reviewMovie, 50m);
    if (!reviewModel.ShowHeroReviewAction)
    {
        throw new Exception("Expected watched movies that still need review to keep the hero review action.");
    }

    var unwatchedMovie = new Movie
    {
        Title = "Unwatched",
        MediaType = MediaType.Movie,
        WatchedStatus = WatchedStatus.NotWatched,
        PredictedScore = 72m
    };
    var unwatchedModel = MovieDetailsViewModel.Create(unwatchedMovie, 50m);
    if (!unwatchedModel.ShowHeroReviewAction)
    {
        throw new Exception("Expected unwatched movies to show the hero review action.");
    }
}

static async Task AssertMovieDetailsDetailsActionLoadsReferenceMovieAsync()
{
    await using var connection = new SqliteConnection("Data Source=:memory:");
    await connection.OpenAsync();

    var options = new DbContextOptionsBuilder<AppDbContext>()
        .UseSqlite(connection)
        .Options;

    await using var dbContext = new AppDbContext(options);
    await dbContext.Database.EnsureCreatedAsync();

    var referenceMovie = new Movie
    {
        Title = "Wandering Earth 2",
        NormalizedTitle = TitleNormalizer.Normalize("Wandering Earth 2"),
        MediaType = MediaType.Movie,
        WatchedStatus = WatchedStatus.Watched,
        UserRating = 95m,
        UserGrade = UserGrade.Loved,
        PrimaryVerdict = PersonalVerdict.Loved,
        GenresCsv = "Sci-Fi",
        RuntimeMinutes = 173,
        PredictedScore = 96m,
        NormalizedTagsCsv = "Epic scale, Great worldbuilding, Incredible visuals"
    };

    var candidateMovie = new Movie
    {
        Title = "Along with the Gods",
        NormalizedTitle = TitleNormalizer.Normalize("Along with the Gods"),
        MediaType = MediaType.Movie,
        WatchedStatus = WatchedStatus.NotWatched,
        GenresCsv = "Fantasy",
        RuntimeMinutes = 139,
        PredictedScore = 26.8m,
        RecommendationContextJson = """
        {
          "similarToLiked": [
            {
              "title": "Wandering Earth 2",
              "verdict": 1,
              "similarityScore": 1.5
            }
          ]
        }
        """
    };

    dbContext.Movies.AddRange(referenceMovie, candidateMovie);
    await dbContext.SaveChangesAsync();

    var controller = CreateMoviesController(dbContext, CreatePreferencesService());
    var result = await controller.Details(candidateMovie.Id, CancellationToken.None);
    if (result is not ViewResult viewResult || viewResult.Model is not MovieDetailsViewModel model)
    {
        throw new Exception("Expected details action to return the movie details view model.");
    }

    if (!string.Equals(model.ReferenceMovie?.Title, "Wandering Earth 2", StringComparison.Ordinal))
    {
        throw new Exception($"Expected details action to resolve the comparison movie, found '{model.ReferenceMovie?.Title ?? "<null>"}'.");
    }
}

static void AssertWatchModalSupportsReviewLater()
{
    var path = Path.Combine(FindProjectRoot(), "src", "LocalMovieVault.Web", "Views", "Shared", "_WatchFeedbackModal.cshtml");
    var content = File.ReadAllText(path);

    if (!content.Contains("Review later", StringComparison.Ordinal) || !content.Contains("watchFeedbackReviewLater", StringComparison.Ordinal))
    {
        throw new Exception("Expected watch feedback modal to support the Review later action.");
    }
}

static void AssertGenreNormalizerSupportsPipeSeparatedGenres()
{
    var path = Path.Combine(FindProjectRoot(), "src", "LocalMovieVault.Web", "wwwroot", "js", "app-main.js");
    var content = File.ReadAllText(path);

    if (!content.Contains("split(/[;,/|]/)", StringComparison.Ordinal))
    {
        throw new Exception("Expected genre normalizer to support pipe-delimited genres.");
    }
}

static void AssertDiscoveryCardDoesNotShowPredictionNarrative()
{
    var path = Path.Combine(FindProjectRoot(), "src", "LocalMovieVault.Web", "Views", "Shared", "_DiscoveryCard.cshtml");
    var content = File.ReadAllText(path);

    if (content.Contains("Model.PredictedReason", StringComparison.Ordinal) ||
        content.Contains("Open the review to save your own notes and tags.", StringComparison.Ordinal))
    {
        throw new Exception("Expected discovery cards to omit prediction narrative text.");
    }
}

static void AssertToggleUnwatchedFallsBackToFormPost()
{
    var path = Path.Combine(FindProjectRoot(), "src", "LocalMovieVault.Web", "wwwroot", "js", "app-main.js");
    var content = File.ReadAllText(path);

    if (!content.Contains("submitFallbackPost('/Movies/ToggleWatched'", StringComparison.Ordinal) ||
        !content.Contains("response.json().catch", StringComparison.Ordinal))
    {
        throw new Exception("Expected unwatched toggle flow to fall back to a regular POST when the AJAX path fails.");
    }
}

static void AssertToggleUnwatchedUsesDedicatedToggleForm()
{
    var appMainPath = Path.Combine(FindProjectRoot(), "src", "LocalMovieVault.Web", "wwwroot", "js", "app-main.js");
    var appMainContent = File.ReadAllText(appMainPath);
    if (!appMainContent.Contains("toggleWatchedForm", StringComparison.Ordinal))
    {
        throw new Exception("Expected unwatched toggle script to use a dedicated toggle form.");
    }

    var modalPath = Path.Combine(FindProjectRoot(), "src", "LocalMovieVault.Web", "Views", "Shared", "_WatchFeedbackModal.cshtml");
    var modalContent = File.ReadAllText(modalPath);
    if (!modalContent.Contains("id=\"toggleWatchedForm\"", StringComparison.Ordinal) ||
        !modalContent.Contains("asp-action=\"ToggleWatched\"", StringComparison.Ordinal))
    {
        throw new Exception("Expected shared watch modal markup to include a dedicated ToggleWatched form.");
    }
}

static async Task AssertCsvDeltaParserAsync()
{
    const string csv = "Title,Year,LegacyUserScore,UserGrade,NewTagsCsv,Action,ReviewStatus,Source,OriginalTagsCsv,DroppedLegacyTagsCsv,Notes\n\"Example\",2024,80,Liked,\"Great acting, Original idea\",UpdateExisting,ReadyForUpdate,CSV,,,note";
    await using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(csv));
    var parser = new CsvMovieDeltaImportService();
    var rows = await parser.ParseAsync(stream);
    if (rows.Count != 1 || rows[0].Title != "Example" || rows[0].Year != 2024)
    {
        throw new Exception("CSV delta parser did not return the expected row.");
    }
}

static async Task AssertRecommendationUsesNormalizedTagsAsync()
{
    await using var connection = new SqliteConnection("Data Source=:memory:");
    await connection.OpenAsync();

    var options = new DbContextOptionsBuilder<AppDbContext>()
        .UseSqlite(connection)
        .Options;

    await using var dbContext = new AppDbContext(options);
    await dbContext.Database.EnsureCreatedAsync();

    dbContext.Movies.AddRange(
        new Movie
        {
            Title = "Loved Benchmark",
            MediaType = MediaType.Movie,
            WatchedStatus = WatchedStatus.Watched,
            UserGrade = UserGrade.Loved,
            PrimaryVerdict = PersonalVerdict.Loved,
            UserRating = 95m,
            GenresCsv = "Thriller, Mystery",
            NormalizedTagsCsv = "Great twist, Original idea, Great acting",
            Overview = "An inventive thriller with a shocking reveal and strong performances.",
            Year = 2024,
            RuntimeMinutes = 110,
            Language = "English",
            Country = "USA"
        },
        new Movie
        {
            Title = "Candidate",
            MediaType = MediaType.Movie,
            WatchedStatus = WatchedStatus.NotWatched,
            GenresCsv = "Thriller, Mystery",
            TmdbKeywordsCsv = "twist, reveal, performance",
            Overview = "A mystery built around a major reveal with strong performances.",
            Year = 2025,
            RuntimeMinutes = 108,
            Language = "English",
            Country = "USA"
        });

    await dbContext.SaveChangesAsync();

    var settingsPath = Path.Combine(Path.GetTempPath(), $"mymoviedb-test-settings-{Guid.NewGuid():N}.json");
    var engine = new DeterministicRecommendationEngine(
        dbContext,
        new RecommendationFeatureExtractor(new PlotKeywordExtractor()),
        new RecommendationExplainer(),
        new AppUserPreferencesService(new AppStorageOptions
        {
            DataHomePath = Path.GetTempPath(),
            DatabasePath = "memory",
            SeedPath = "seed",
            SettingsPath = settingsPath
        }));

    await engine.RecalculateAsync();

    var candidate = await dbContext.Movies.SingleAsync(x => x.Title == "Candidate");
    if ((candidate.PredictedScore ?? 0m) <= 0m)
    {
        throw new Exception("Expected candidate to receive a predicted score.");
    }

    if (string.IsNullOrWhiteSpace(candidate.PredictedLabel))
    {
        throw new Exception("Expected candidate to receive a predicted grade label.");
    }
}

static async Task AssertSingleWatchedMovieDoesNotSelfBoostAsync()
{
    await using var connection = new SqliteConnection("Data Source=:memory:");
    await connection.OpenAsync();

    var options = new DbContextOptionsBuilder<AppDbContext>()
        .UseSqlite(connection)
        .Options;

    await using var dbContext = new AppDbContext(options);
    await dbContext.Database.EnsureCreatedAsync();

    dbContext.Movies.Add(new Movie
    {
        Title = "Solo Evidence",
        MediaType = MediaType.Movie,
        WatchedStatus = WatchedStatus.Watched,
        UserGrade = UserGrade.Loved,
        PrimaryVerdict = PersonalVerdict.Loved,
        UserRating = 95m,
        GenresCsv = "Sci-Fi, Thriller",
        NormalizedTagsCsv = "Original idea, Great twist, Incredible visuals",
        Overview = "An inventive sci-fi thriller with a big reveal and striking visuals.",
        Year = 2024,
        RuntimeMinutes = 98,
        ImdbRating = 7.5m,
        ImdbVotes = 50000
    });

    await dbContext.SaveChangesAsync();

    var preferences = CreatePreferencesService();
    var engine = new DeterministicRecommendationEngine(
        dbContext,
        new RecommendationFeatureExtractor(new PlotKeywordExtractor()),
        new RecommendationExplainer(),
        preferences);

    await engine.RecalculateAsync();

    var updated = await dbContext.Movies.SingleAsync(x => x.Title == "Solo Evidence");
    if ((updated.PersonalMatchScore ?? 0m) > 60m)
    {
        throw new Exception($"Expected a lone watched movie to avoid self-boosting its own match score, found {updated.PersonalMatchScore:0.0}.");
    }
}

static async Task AssertMehRatingDoesNotPenalizeMatchingCandidateAsync()
{
    await using var connection = new SqliteConnection("Data Source=:memory:");
    await connection.OpenAsync();

    var options = new DbContextOptionsBuilder<AppDbContext>()
        .UseSqlite(connection)
        .Options;

    await using var dbContext = new AppDbContext(options);
    await dbContext.Database.EnsureCreatedAsync();

    dbContext.Movies.AddRange(
        new Movie
        {
            Title = "Loved Anchor",
            MediaType = MediaType.Movie,
            WatchedStatus = WatchedStatus.Watched,
            UserGrade = UserGrade.Loved,
            PrimaryVerdict = PersonalVerdict.Loved,
            UserRating = 95m,
            GenresCsv = "Sci-Fi, Thriller",
            NormalizedTagsCsv = "Original idea, Great twist, Incredible visuals",
            Overview = "An inventive sci-fi thriller with striking visuals and a major reveal.",
            Year = 2024
        },
        new Movie
        {
            Title = "Meh Neighbor",
            MediaType = MediaType.Movie,
            WatchedStatus = WatchedStatus.Watched,
            UserGrade = UserGrade.Meh,
            PrimaryVerdict = PersonalVerdict.Okay,
            UserRating = 55m,
            GenresCsv = "Sci-Fi, Thriller",
            NormalizedTagsCsv = "Too slow, Weak dialogue, Confusing",
            Overview = "A slow science-fiction mystery with uneven dialogue.",
            Year = 2023
        },
        new Movie
        {
            Title = "Target Candidate",
            MediaType = MediaType.Movie,
            WatchedStatus = WatchedStatus.NotWatched,
            GenresCsv = "Sci-Fi, Thriller",
            TmdbKeywordsCsv = "inventive, reveal, visuals",
            Overview = "An inventive thriller with striking visuals and a reveal.",
            Year = 2025
        });

    await dbContext.SaveChangesAsync();

    var engine = new DeterministicRecommendationEngine(
        dbContext,
        new RecommendationFeatureExtractor(new PlotKeywordExtractor()),
        new RecommendationExplainer(),
        CreatePreferencesService());

    var results = engine.CalculateResults(await dbContext.Movies.ToListAsync(), AppUserPreferences.CreateDefault());
    var candidate = await dbContext.Movies.SingleAsync(x => x.Title == "Target Candidate");
    var predictedScore = results[candidate.Id].FinalScore;

    if (predictedScore < 80m)
    {
        throw new Exception($"Expected a matching candidate to stay strong even with a Meh neighbor, found {predictedScore:0.0}.");
    }
}

static async Task AssertStrongCandidateCanScoreHighWithoutPerfectTagOverlapAsync()
{
    await using var connection = new SqliteConnection("Data Source=:memory:");
    await connection.OpenAsync();

    var options = new DbContextOptionsBuilder<AppDbContext>()
        .UseSqlite(connection)
        .Options;

    await using var dbContext = new AppDbContext(options);
    await dbContext.Database.EnsureCreatedAsync();

    dbContext.Movies.AddRange(
        new Movie
        {
            Title = "Loved Benchmark",
            MediaType = MediaType.Movie,
            WatchedStatus = WatchedStatus.Watched,
            UserGrade = UserGrade.Loved,
            PrimaryVerdict = PersonalVerdict.Loved,
            UserRating = 95m,
            GenresCsv = "Thriller, Mystery",
            NormalizedTagsCsv = "Great twist, Original idea, Great acting",
            Overview = "An inventive thriller with a shocking reveal and strong performances.",
            Year = 2024,
            RuntimeMinutes = 110,
            Language = "English",
            Country = "USA"
        },
        new Movie
        {
            Title = "High Potential Candidate",
            MediaType = MediaType.Movie,
            WatchedStatus = WatchedStatus.NotWatched,
            GenresCsv = "Thriller, Mystery",
            TmdbKeywordsCsv = "reveal, performance",
            Overview = "A mystery built around a major reveal with strong performances and careful plotting.",
            Year = 2025,
            RuntimeMinutes = 108,
            Language = "English",
            Country = "USA"
        });

    await dbContext.SaveChangesAsync();

    var engine = new DeterministicRecommendationEngine(
        dbContext,
        new RecommendationFeatureExtractor(new PlotKeywordExtractor()),
        new RecommendationExplainer(),
        CreatePreferencesService());

    var results = engine.CalculateResults(await dbContext.Movies.ToListAsync(), AppUserPreferences.CreateDefault());
    var candidate = await dbContext.Movies.SingleAsync(x => x.Title == "High Potential Candidate");
    var predictedScore = results[candidate.Id].FinalScore;

    if (predictedScore < 82m)
    {
        throw new Exception($"Expected a strong candidate to reach a convincingly high score without perfect explicit tag overlap, found {predictedScore:0.0}.");
    }
}

static async Task AssertLowScoreExplanationDoesNotSoundPositiveAsync()
{
    await using var connection = new SqliteConnection("Data Source=:memory:");
    await connection.OpenAsync();

    var options = new DbContextOptionsBuilder<AppDbContext>()
        .UseSqlite(connection)
        .Options;

    await using var dbContext = new AppDbContext(options);
    await dbContext.Database.EnsureCreatedAsync();

    dbContext.Movies.AddRange(
        new Movie
        {
            Title = "Epic Sci-Fi Anchor",
            MediaType = MediaType.Movie,
            WatchedStatus = WatchedStatus.Watched,
            UserGrade = UserGrade.Loved,
            PrimaryVerdict = PersonalVerdict.Loved,
            UserRating = 95m,
            GenresCsv = "Sci-Fi",
            NormalizedTagsCsv = "Epic scale, Great worldbuilding, Incredible visuals",
            Overview = "A giant science fiction rescue mission on a grand scale.",
            Year = 2023,
            RuntimeMinutes = 173
        },
        new Movie
        {
            Title = "Fantasy Mismatch",
            MediaType = MediaType.Movie,
            WatchedStatus = WatchedStatus.NotWatched,
            GenresCsv = "Fantasy",
            Overview = "Afterlife guides escort a hero through judgment and mythic trials.",
            Year = 2017,
            RuntimeMinutes = 139
        });

    await dbContext.SaveChangesAsync();

    var engine = new DeterministicRecommendationEngine(
        dbContext,
        new RecommendationFeatureExtractor(new PlotKeywordExtractor()),
        new RecommendationExplainer(),
        CreatePreferencesService());

    var results = engine.CalculateResults(await dbContext.Movies.ToListAsync(), AppUserPreferences.CreateDefault());
    var candidate = await dbContext.Movies.SingleAsync(x => x.Title == "Fantasy Mismatch");
    var reason = results[candidate.Id].PredictedReason;
    var score = results[candidate.Id].FinalScore;

    if (score >= 35m)
    {
        throw new Exception($"Expected low-score fixture to stay below the reject threshold, found {score:0.0}.");
    }

    if (reason.Contains("Likely to work", StringComparison.OrdinalIgnoreCase) ||
        reason.Contains("Could work", StringComparison.OrdinalIgnoreCase))
    {
        throw new Exception($"Expected low-score explanation to avoid positive framing, found '{reason}'.");
    }

    if (!reason.Contains("Probably not a strong fit", StringComparison.OrdinalIgnoreCase))
    {
        throw new Exception($"Expected low-score explanation to lead with a blocker-oriented summary, found '{reason}'.");
    }
}

static async Task AssertNeedsTagReviewMovieDoesNotTrainRecommendationsAsync()
{
    await using var connection = new SqliteConnection("Data Source=:memory:");
    await connection.OpenAsync();

    var options = new DbContextOptionsBuilder<AppDbContext>()
        .UseSqlite(connection)
        .Options;

    await using var dbContext = new AppDbContext(options);
    await dbContext.Database.EnsureCreatedAsync();

    dbContext.Movies.AddRange(
        new Movie
        {
            Title = "Complete Anchor",
            MediaType = MediaType.Movie,
            WatchedStatus = WatchedStatus.Watched,
            UserGrade = UserGrade.Loved,
            PrimaryVerdict = PersonalVerdict.Loved,
            UserRating = 95m,
            GenresCsv = "Sci-Fi, Thriller",
            NormalizedTagsCsv = "Original idea, Great acting, Incredible visuals",
            Overview = "A smart science fiction thriller with strong performances.",
            Year = 2024
        },
        new Movie
        {
            Title = "Incomplete Anchor",
            MediaType = MediaType.Movie,
            WatchedStatus = WatchedStatus.Watched,
            UserGrade = UserGrade.Loved,
            PrimaryVerdict = PersonalVerdict.Loved,
            UserRating = 95m,
            GenresCsv = "Fantasy, Adventure",
            NeedsTagReview = true,
            Overview = "A giant fantasy adventure with epic set pieces.",
            Year = 2024
        },
        new Movie
        {
            Title = "Candidate",
            MediaType = MediaType.Movie,
            WatchedStatus = WatchedStatus.NotWatched,
            GenresCsv = "Fantasy, Adventure",
            Overview = "A fantasy rescue story built around giant-scale spectacle.",
            Year = 2025
        });

    await dbContext.SaveChangesAsync();

    var engine = new DeterministicRecommendationEngine(
        dbContext,
        new RecommendationFeatureExtractor(new PlotKeywordExtractor()),
        new RecommendationExplainer(),
        CreatePreferencesService());

    var results = engine.CalculateResults(await dbContext.Movies.ToListAsync(), AppUserPreferences.CreateDefault());
    var candidate = await dbContext.Movies.SingleAsync(x => x.Title == "Candidate");
    var context = results[candidate.Id].Context;

    if (context.SimilarToLiked.Any(x => string.Equals(x.Title, "Incomplete Anchor", StringComparison.Ordinal)))
    {
        throw new Exception("Expected NeedsTagReview watched movies to be excluded from liked similarity anchors.");
    }
}

static async Task AssertScoreCalibrationDoesNotOveruseTopBandAsync()
{
    SmokeTrace("AssertScoreCalibrationDoesNotOveruseTopBandAsync: start");
    await using var connection = new SqliteConnection("Data Source=:memory:");
    await connection.OpenAsync();
    SmokeTrace("AssertScoreCalibrationDoesNotOveruseTopBandAsync: connection-open");

    var options = new DbContextOptionsBuilder<AppDbContext>()
        .UseSqlite(connection)
        .Options;

    await using var dbContext = new AppDbContext(options);
    await dbContext.Database.EnsureCreatedAsync();
    SmokeTrace("AssertScoreCalibrationDoesNotOveruseTopBandAsync: db-created");

    dbContext.Movies.AddRange(
        new Movie
        {
            Title = "Loved Anchor",
            MediaType = MediaType.Movie,
            WatchedStatus = WatchedStatus.Watched,
            UserGrade = UserGrade.Loved,
            PrimaryVerdict = PersonalVerdict.Loved,
            UserRating = 95m,
            GenresCsv = "Thriller, Mystery",
            NormalizedTagsCsv = "Great twist, Original idea, Great acting",
            Overview = "An inventive thriller with a shocking reveal.",
            Year = 2024
        },
        new Movie
        {
            Title = "Strong Candidate",
            MediaType = MediaType.Movie,
            WatchedStatus = WatchedStatus.NotWatched,
            GenresCsv = "Thriller, Mystery",
            TmdbKeywordsCsv = "reveal, performance",
            Overview = "A mystery built around a major reveal with strong performances.",
            Year = 2025
        },
        new Movie
        {
            Title = "Average Candidate",
            MediaType = MediaType.Movie,
            WatchedStatus = WatchedStatus.NotWatched,
            GenresCsv = "Comedy",
            Overview = "A casual workplace comedy with light banter.",
            Year = 2025
        });

    await dbContext.SaveChangesAsync();
    SmokeTrace("AssertScoreCalibrationDoesNotOveruseTopBandAsync: seed-saved");

    var engine = new DeterministicRecommendationEngine(
        dbContext,
        new RecommendationFeatureExtractor(new PlotKeywordExtractor()),
        new RecommendationExplainer(),
        CreatePreferencesService());

    var results = engine.CalculateResults(await dbContext.Movies.ToListAsync(), AppUserPreferences.CreateDefault());
    SmokeTrace("AssertScoreCalibrationDoesNotOveruseTopBandAsync: results-calculated");
    var strongCandidate = await dbContext.Movies.SingleAsync(x => x.Title == "Strong Candidate");
    var averageCandidate = await dbContext.Movies.SingleAsync(x => x.Title == "Average Candidate");
    SmokeTrace("AssertScoreCalibrationDoesNotOveruseTopBandAsync: candidates-loaded");
    var strongScore = results[strongCandidate.Id].FinalScore;
    var averageScore = results[averageCandidate.Id].FinalScore;
    SmokeTrace($"AssertScoreCalibrationDoesNotOveruseTopBandAsync: scores strong={strongScore:0.0} average={averageScore:0.0}");

    if (strongScore >= 96m)
    {
        throw new Exception($"Expected top-band calibration to stop defaulting strong candidates to 96+, found {strongScore:0.0}.");
    }

    if (averageScore >= 90m)
    {
        throw new Exception($"Expected average candidates to stay out of the inflated top band, found {averageScore:0.0}.");
    }

    SmokeTrace("AssertScoreCalibrationDoesNotOveruseTopBandAsync: end");
}

static void AssertImportantTagsMigrateToCanonicalArray()
{
    var settingsPath = Path.Combine(Path.GetTempPath(), $"mymoviedb-test-settings-{Guid.NewGuid():N}.json");
    var root = new JsonObject
    {
        ["Preferences"] = new JsonObject
        {
            ["ImportantTags"] = new JsonArray(" Tense ", "Beautiful visuals", "Tense", "Original idea", "Scary")
        }
    };
    File.WriteAllText(settingsPath, root.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));

    var service = new AppUserPreferencesService(new AppStorageOptions
    {
        DataHomePath = Path.GetTempPath(),
        DatabasePath = "memory",
        SeedPath = "seed",
        SettingsPath = settingsPath
    });

    var preferences = service.Get();
    var tags = preferences.GetImportantTags();
    var expected = new[] { "Tense", "Incredible visuals", "Original idea", "Scary" };
    if (!tags.SequenceEqual(expected, StringComparer.Ordinal))
    {
        throw new Exception($"Expected canonical important tags [{string.Join(", ", expected)}], found [{string.Join(", ", tags)}].");
    }

    service.Save(preferences);
    var json = File.ReadAllText(settingsPath);
    if (!json.Contains("\"ImportantTags\"", StringComparison.Ordinal) ||
        json.Contains("\"ImportantTag1\"", StringComparison.Ordinal))
    {
        throw new Exception("Expected preferences save to persist a canonical ImportantTags array without legacy slot fields.");
    }
}

static async Task AssertImportantTagsBoostMatchingCandidateMoreThanGenericAsync()
{
    await using var connection = new SqliteConnection("Data Source=:memory:");
    await connection.OpenAsync();

    var options = new DbContextOptionsBuilder<AppDbContext>()
        .UseSqlite(connection)
        .Options;

    await using var dbContext = new AppDbContext(options);
    await dbContext.Database.EnsureCreatedAsync();

    dbContext.Movies.AddRange(
        new Movie
        {
            Title = "Loved Benchmark",
            MediaType = MediaType.Movie,
            WatchedStatus = WatchedStatus.Watched,
            UserGrade = UserGrade.Loved,
            PrimaryVerdict = PersonalVerdict.Loved,
            UserRating = 95m,
            GenresCsv = "Sci-Fi, Thriller",
            NormalizedTagsCsv = "Original idea, Incredible visuals, Great twist, Tense",
            Overview = "An inventive, visually striking thriller with constant tension and a major reveal.",
            Year = 2024,
            RuntimeMinutes = 118,
            Language = "English",
            Country = "USA"
        },
        new Movie
        {
            Title = "Matching Candidate",
            MediaType = MediaType.Movie,
            WatchedStatus = WatchedStatus.NotWatched,
            GenresCsv = "Sci-Fi, Thriller",
            NormalizedTagsCsv = "Original idea, Incredible visuals, Great twist",
            TmdbKeywordsCsv = "inventive, reveal, tension, visuals",
            Overview = "A visually striking sci-fi thriller built around an original idea, a reveal, and relentless tension.",
            Year = 2025,
            RuntimeMinutes = 116,
            Language = "English",
            Country = "USA"
        },
        new Movie
        {
            Title = "Generic Candidate",
            MediaType = MediaType.Movie,
            WatchedStatus = WatchedStatus.NotWatched,
            GenresCsv = "Sci-Fi, Thriller",
            TmdbKeywordsCsv = "future, city, detective",
            Overview = "A generic detective story set in the future.",
            Year = 2025,
            RuntimeMinutes = 110,
            Language = "English",
            Country = "USA"
        });

    await dbContext.SaveChangesAsync();

    var engine = new DeterministicRecommendationEngine(
        dbContext,
        new RecommendationFeatureExtractor(new PlotKeywordExtractor()),
        new RecommendationExplainer(),
        CreatePreferencesService());

    var movies = await dbContext.Movies.ToListAsync();
    var baselineResults = engine.CalculateResults(movies, AppUserPreferences.CreateDefault());
    var boosted = CreatePreferencesWithImportantTags("Original idea", "Incredible visuals", "Great twist", "Tense");
    var boostedResults = engine.CalculateResults(movies, boosted);
    var matchingMovie = movies.Single(x => x.Title == "Matching Candidate");
    var genericMovie = movies.Single(x => x.Title == "Generic Candidate");
    var baselineMatchingScore = baselineResults[matchingMovie.Id].FinalScore;
    var baselineGenericScore = baselineResults[genericMovie.Id].FinalScore;
    var updatedMatchingScore = boostedResults[matchingMovie.Id].FinalScore;
    var updatedGenericScore = boostedResults[genericMovie.Id].FinalScore;
    var matchingDelta = updatedMatchingScore - baselineMatchingScore;
    var genericDelta = updatedGenericScore - baselineGenericScore;
    var matchingContext = string.Join(", ", boostedResults[matchingMovie.Id].Context.PositiveFactors.Select(x => $"{x.Label}={x.Weight:0.0}").Take(8));
    var matchingWarnings = string.Join(", ", boostedResults[matchingMovie.Id].Context.WarningFactors);

    if (matchingDelta < 4m || matchingDelta <= genericDelta + 2m)
    {
        throw new Exception($"Expected important tags to materially boost the matching candidate. Matching {baselineMatchingScore:0.0}->{updatedMatchingScore:0.0}, generic {baselineGenericScore:0.0}->{updatedGenericScore:0.0}, tags=[{string.Join(", ", boosted.GetImportantTags())}], warnings=[{matchingWarnings}], factors=[{matchingContext}].");
    }
}

static async Task AssertSettingsPreviewUsesTemporaryImportantTagsWithoutPersistingAsync()
{
    await using var connection = new SqliteConnection("Data Source=:memory:");
    await connection.OpenAsync();

    var options = new DbContextOptionsBuilder<AppDbContext>()
        .UseSqlite(connection)
        .Options;

    await using var dbContext = new AppDbContext(options);
    await dbContext.Database.EnsureCreatedAsync();

    dbContext.Movies.AddRange(
        new Movie
        {
            Title = "Loved Benchmark",
            MediaType = MediaType.Movie,
            WatchedStatus = WatchedStatus.Watched,
            UserGrade = UserGrade.Loved,
            PrimaryVerdict = PersonalVerdict.Loved,
            UserRating = 95m,
            GenresCsv = "Sci-Fi, Thriller",
            NormalizedTagsCsv = "Original idea, Great twist, Incredible visuals, Tense",
            Overview = "An inventive and tense sci-fi thriller with a big reveal and stunning visuals.",
            Year = 2024
        },
        new Movie
        {
            Title = "Preview Candidate",
            MediaType = MediaType.Movie,
            WatchedStatus = WatchedStatus.NotWatched,
            GenresCsv = "Sci-Fi, Thriller",
            NormalizedTagsCsv = "Original idea, Great twist, Incredible visuals",
            TmdbKeywordsCsv = "inventive, reveal, tension, visuals",
            Overview = "A tense thriller with inventive visuals and a reveal.",
            Year = 2025
        });

    await dbContext.SaveChangesAsync();

    var settingsPath = Path.Combine(Path.GetTempPath(), $"mymoviedb-test-settings-{Guid.NewGuid():N}.json");
    var preferencesService = new AppUserPreferencesService(new AppStorageOptions
    {
        DataHomePath = Path.GetTempPath(),
        DatabasePath = "memory",
        SeedPath = "seed",
        SettingsPath = settingsPath
    });
    var engine = new DeterministicRecommendationEngine(
        dbContext,
        new RecommendationFeatureExtractor(new PlotKeywordExtractor()),
        new RecommendationExplainer(),
        preferencesService);
    await engine.RecalculateAsync();

    var candidate = await dbContext.Movies.SingleAsync(x => x.Title == "Preview Candidate");
    var currentPredictedScore = candidate.PredictedScore ?? 0m;

    var controller = new SettingsController(
        dbContext,
        preferencesService,
        new PersonalMatchService(engine),
        new RecommendationPreviewService(dbContext, preferencesService, engine));

    var previewPreferences = CreatePreferencesWithImportantTags("Original idea", "Great twist", "Incredible visuals", "Tense");
    var result = await controller.Preview(previewPreferences, previewPreferences.GetImportantTags().ToList(), candidate.Id, CancellationToken.None);
    if (result is not PartialViewResult partial || partial.Model is not AppSettingsViewModel model)
    {
        throw new Exception("Expected settings preview to return a partial view model.");
    }

    if (model.PreviewPredictedScore <= currentPredictedScore)
    {
        throw new Exception($"Expected temporary important tags to raise preview score above {currentPredictedScore:0.0}, found {model.PreviewPredictedScore:0.0}.");
    }

    if (preferencesService.Get().GetImportantTags().Count != 0)
    {
        throw new Exception("Expected preview to avoid persisting temporary important tags.");
    }
}

static async Task AssertSettingsSavePersistsImportantTagsAndRecalculatesAsync()
{
    await using var connection = new SqliteConnection("Data Source=:memory:");
    await connection.OpenAsync();

    var options = new DbContextOptionsBuilder<AppDbContext>()
        .UseSqlite(connection)
        .Options;

    await using var dbContext = new AppDbContext(options);
    await dbContext.Database.EnsureCreatedAsync();

    dbContext.Movies.AddRange(
        new Movie
        {
            Title = "Loved Benchmark",
            MediaType = MediaType.Movie,
            WatchedStatus = WatchedStatus.Watched,
            UserGrade = UserGrade.Loved,
            PrimaryVerdict = PersonalVerdict.Loved,
            UserRating = 95m,
            GenresCsv = "Sci-Fi, Thriller",
            NormalizedTagsCsv = "Original idea, Great twist, Incredible visuals, Tense",
            Overview = "An inventive and tense sci-fi thriller with a big reveal and stunning visuals.",
            Year = 2024
        },
        new Movie
        {
            Title = "Save Candidate",
            MediaType = MediaType.Movie,
            WatchedStatus = WatchedStatus.NotWatched,
            GenresCsv = "Sci-Fi, Thriller",
            NormalizedTagsCsv = "Original idea, Great twist, Incredible visuals",
            TmdbKeywordsCsv = "inventive, reveal, tension, visuals",
            Overview = "A tense thriller with inventive visuals and a reveal.",
            Year = 2025
        });

    await dbContext.SaveChangesAsync();

    var settingsPath = Path.Combine(Path.GetTempPath(), $"mymoviedb-test-settings-{Guid.NewGuid():N}.json");
    var preferencesService = new AppUserPreferencesService(new AppStorageOptions
    {
        DataHomePath = Path.GetTempPath(),
        DatabasePath = "memory",
        SeedPath = "seed",
        SettingsPath = settingsPath
    });
    var engine = new DeterministicRecommendationEngine(
        dbContext,
        new RecommendationFeatureExtractor(new PlotKeywordExtractor()),
        new RecommendationExplainer(),
        preferencesService);
    await engine.RecalculateAsync();

    var baselineScore = (await dbContext.Movies.SingleAsync(x => x.Title == "Save Candidate")).PredictedScore ?? 0m;
    var controller = new SettingsController(
        dbContext,
        preferencesService,
        new PersonalMatchService(engine),
        new RecommendationPreviewService(dbContext, preferencesService, engine));

    var selectedPreferences = CreatePreferencesWithImportantTags("Original idea", "Great twist", "Incredible visuals", "Tense");
    var model = new AppSettingsViewModel
    {
        Preferences = selectedPreferences,
        Genres = [],
        ImportantTagOptions = [],
        TastePrioritySelections = selectedPreferences.GetImportantTags().ToList()
    };

    var result = await controller.Save(model, CancellationToken.None);
    if (result is not RedirectToActionResult redirect || !string.Equals(redirect.ActionName, "Index", StringComparison.Ordinal))
    {
        throw new Exception("Expected settings save to redirect back to Index.");
    }

    var persistedTags = preferencesService.Get().GetImportantTags();
    if (persistedTags.Count != 4)
    {
        throw new Exception($"Expected settings save to persist 4 important tags, found {persistedTags.Count}.");
    }

    var json = File.ReadAllText(settingsPath);
    if (!json.Contains("\"ImportantTags\"", StringComparison.Ordinal))
    {
        throw new Exception("Expected settings save to write ImportantTags to the settings file.");
    }

    var updatedScore = (await dbContext.Movies.SingleAsync(x => x.Title == "Save Candidate")).PredictedScore ?? 0m;
    if (updatedScore <= baselineScore)
    {
        throw new Exception($"Expected settings save to recalculate predictions upward from {baselineScore:0.0}, found {updatedScore:0.0}.");
    }
}

static async Task AssertSettingsSaveUsesSelectionsWhenPreferencesAreNotPostedAsync()
{
    await using var connection = new SqliteConnection("Data Source=:memory:");
    await connection.OpenAsync();

    var options = new DbContextOptionsBuilder<AppDbContext>()
        .UseSqlite(connection)
        .Options;

    await using var dbContext = new AppDbContext(options);
    var migrator = new DatabaseSchemaMigrator();
    await migrator.MigrateAsync(dbContext);

    dbContext.Movies.AddRange(
        new Movie
        {
            Title = "Settings Benchmark",
            MediaType = MediaType.Movie,
            WatchedStatus = WatchedStatus.Watched,
            UserGrade = UserGrade.Loved,
            PrimaryVerdict = PersonalVerdict.Loved,
            UserRating = 95m,
            GenresCsv = "Sci-Fi, Thriller",
            NormalizedTagsCsv = "Original idea, Great twist, Incredible visuals, Tense",
            Overview = "An inventive and tense sci-fi thriller with a big reveal and stunning visuals.",
            Year = 2024
        },
        new Movie
        {
            Title = "Settings Candidate",
            MediaType = MediaType.Movie,
            WatchedStatus = WatchedStatus.NotWatched,
            GenresCsv = "Sci-Fi, Thriller",
            NormalizedTagsCsv = "Original idea, Great twist, Incredible visuals",
            TmdbKeywordsCsv = "inventive, reveal, tension, visuals",
            Overview = "A tense thriller with inventive visuals and a reveal.",
            Year = 2025
        });

    await dbContext.SaveChangesAsync();

    var preferencesService = CreatePreferencesService();
    var engine = new DeterministicRecommendationEngine(
        dbContext,
        new RecommendationFeatureExtractor(new PlotKeywordExtractor()),
        new RecommendationExplainer(),
        preferencesService);
    await engine.RecalculateAsync();

    var controller = new SettingsController(
        dbContext,
        preferencesService,
        new PersonalMatchService(engine),
        new RecommendationPreviewService(dbContext, preferencesService, engine),
        new AppEventLogService(dbContext));
    controller.ControllerContext = new ControllerContext
    {
        HttpContext = new DefaultHttpContext()
    };
    controller.TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(
        controller.HttpContext,
        new FakeTempDataProvider());

    var result = await controller.Save(new AppSettingsViewModel
    {
        Preferences = null!,
        Genres = [],
        ImportantTagOptions = [],
        TastePrioritySelections = ["Original idea", "Great twist", "Incredible visuals", "Tense"]
    }, CancellationToken.None);

    if (result is not RedirectToActionResult redirect || !string.Equals(redirect.ActionName, "Index", StringComparison.Ordinal))
    {
        throw new Exception("Expected settings save without posted Preferences to redirect back to Index.");
    }

    var persistedTags = preferencesService.Get().GetImportantTags();
    if (persistedTags.Count != 4)
    {
        throw new Exception($"Expected settings save without posted Preferences to persist 4 tags, found {persistedTags.Count}.");
    }

    var eventCount = await GetAppEventCountAsync(connection, "Settings.Save");
    if (eventCount != 1)
    {
        throw new Exception($"Expected one Settings.Save app event, found {eventCount}.");
    }
}

static async Task AssertSettingsPreviewFallsBackWhenPinnedMovieDoesNotChangeAsync()
{
    await using var connection = new SqliteConnection("Data Source=:memory:");
    await connection.OpenAsync();

    var options = new DbContextOptionsBuilder<AppDbContext>()
        .UseSqlite(connection)
        .Options;

    await using var dbContext = new AppDbContext(options);
    await dbContext.Database.EnsureCreatedAsync();

    dbContext.Movies.AddRange(
        new Movie
        {
            Title = "Loved Benchmark",
            MediaType = MediaType.Movie,
            WatchedStatus = WatchedStatus.Watched,
            UserGrade = UserGrade.Loved,
            PrimaryVerdict = PersonalVerdict.Loved,
            UserRating = 95m,
            GenresCsv = "Sci-Fi, Thriller",
            NormalizedTagsCsv = "Original idea, Great twist, Incredible visuals, Tense",
            Overview = "An inventive and tense sci-fi thriller with a big reveal and stunning visuals.",
            Year = 2024
        },
        new Movie
        {
            Title = "Pinned Flat Candidate",
            MediaType = MediaType.Movie,
            WatchedStatus = WatchedStatus.NotWatched,
            GenresCsv = "Comedy",
            TmdbKeywordsCsv = "neighbors, picnic, small town",
            Overview = "A gentle comedy about neighbors planning a picnic in a small town.",
            Year = 2025
        },
        new Movie
        {
            Title = "Responsive Candidate",
            MediaType = MediaType.Movie,
            WatchedStatus = WatchedStatus.NotWatched,
            GenresCsv = "Sci-Fi, Thriller",
            NormalizedTagsCsv = "Original idea, Great twist, Incredible visuals",
            TmdbKeywordsCsv = "inventive, reveal, tension, visuals",
            Overview = "A tense thriller with inventive visuals and a reveal.",
            Year = 2025
        });

    await dbContext.SaveChangesAsync();

    var settingsPath = Path.Combine(Path.GetTempPath(), $"mymoviedb-test-settings-{Guid.NewGuid():N}.json");
    var preferencesService = new AppUserPreferencesService(new AppStorageOptions
    {
        DataHomePath = Path.GetTempPath(),
        DatabasePath = "memory",
        SeedPath = "seed",
        SettingsPath = settingsPath
    });
    var engine = new DeterministicRecommendationEngine(
        dbContext,
        new RecommendationFeatureExtractor(new PlotKeywordExtractor()),
        new RecommendationExplainer(),
        preferencesService);
    await engine.RecalculateAsync();

    var controller = new SettingsController(
        dbContext,
        preferencesService,
        new PersonalMatchService(engine),
        new RecommendationPreviewService(dbContext, preferencesService, engine));

    var previewPreferences = CreatePreferencesWithImportantTags("Original idea", "Great twist", "Incredible visuals", "Tense");
    var pinnedMovie = await dbContext.Movies.SingleAsync(x => x.Title == "Pinned Flat Candidate");
    var result = await controller.Preview(previewPreferences, previewPreferences.GetImportantTags().ToList(), pinnedMovie.Id, CancellationToken.None);

    if (result is not PartialViewResult partial || partial.Model is not AppSettingsViewModel model)
    {
        throw new Exception("Expected pinned settings preview to return a partial view model.");
    }

    if (!string.Equals(model.PreviewMovie?.Title, "Responsive Candidate", StringComparison.Ordinal))
    {
        throw new Exception($"Expected preview to fall back to a responsive candidate, found '{model.PreviewMovie?.Title ?? "<null>"}'.");
    }

    if (model.PreviewPredictedScore <= model.PreviewCurrentPredictedScore)
    {
        throw new Exception($"Expected fallback preview candidate to show a positive projected delta, found {model.PreviewCurrentPredictedScore:0.0}->{model.PreviewPredictedScore:0.0}.");
    }
}

static void AssertSettingsViewUsesTastePrioritiesSection()
{
    var path = Path.Combine(FindProjectRoot(), "src", "LocalMovieVault.Web", "Views", "Settings", "Index.cshtml");
    var content = File.ReadAllText(path);

    if (!content.Contains("Taste Priorities", StringComparison.Ordinal) ||
        !content.Contains("Choose up to 4 tags that matter most to you.", StringComparison.Ordinal))
    {
        throw new Exception("Expected settings view to render the Taste Priorities helper copy.");
    }
}

static void AssertSettingsViewDoesNotExposeDeferredSliders()
{
    var path = Path.Combine(FindProjectRoot(), "src", "LocalMovieVault.Web", "Views", "Settings", "Index.cshtml");
    var content = File.ReadAllText(path);
    var forbiddenMarkers = new[]
    {
        "Dismiss score cutoff",
        "Mismatch popup threshold",
        "Genre fit weight",
        "Story weight",
        "Tag influence",
        "IMDb weight",
        "type=\"range\""
    };

    foreach (var marker in forbiddenMarkers)
    {
        if (content.Contains(marker, StringComparison.Ordinal))
        {
            throw new Exception($"Expected settings view to hide deferred slider control '{marker}'.");
        }
    }
}

static void AssertSettingsViewIncludesRandomizeButtonHook()
{
    var path = Path.Combine(FindProjectRoot(), "src", "LocalMovieVault.Web", "Views", "Settings", "Index.cshtml");
    var jsPath = Path.Combine(FindProjectRoot(), "src", "LocalMovieVault.Web", "wwwroot", "js", "app-main.js");
    var viewContent = File.ReadAllText(path);
    var jsContent = File.ReadAllText(jsPath);

    if (!viewContent.Contains("settingsRandomizeButton", StringComparison.Ordinal) ||
        !jsContent.Contains("settingsRandomizeButton", StringComparison.Ordinal) ||
        !jsContent.Contains("params.delete('previewMovieId')", StringComparison.Ordinal))
    {
        throw new Exception("Expected settings randomize flow to be handled through a JS hook that preserves unsaved taste selections without pinning the same preview movie.");
    }
}

static void AssertSettingsViewIncludesDiagnosticsLink()
{
    var path = Path.Combine(FindProjectRoot(), "src", "LocalMovieVault.Web", "Views", "Settings", "Index.cshtml");
    var content = File.ReadAllText(path);

    if (!content.Contains("Diagnostics", StringComparison.Ordinal) ||
        !content.Contains("asp-action=\"Diagnostics\"", StringComparison.Ordinal))
    {
        throw new Exception("Expected settings view to include a Diagnostics entry point for in-app logging.");
    }
}

static void AssertAddLookupFormIncludesAntiForgeryToken()
{
    var path = Path.Combine(FindProjectRoot(), "src", "LocalMovieVault.Web", "Views", "Movies", "Add.cshtml");
    var content = File.ReadAllText(path);

    if (!content.Contains("@Html.AntiForgeryToken()", StringComparison.Ordinal))
    {
        throw new Exception("Expected the Add movie lookup form to include an anti-forgery token.");
    }
}

static async Task AssertToggleWatchedClearsLegacyScoreAsync()
{
    await using var connection = new SqliteConnection("Data Source=:memory:");
    await connection.OpenAsync();

    var options = new DbContextOptionsBuilder<AppDbContext>()
        .UseSqlite(connection)
        .Options;

    await using var dbContext = new AppDbContext(options);
    await dbContext.Database.EnsureCreatedAsync();

    var movie = new Movie
    {
        Title = "Toggle Test",
        MediaType = MediaType.Movie,
        WatchedStatus = WatchedStatus.Watched,
        UserGrade = UserGrade.Liked,
        PrimaryVerdict = PersonalVerdict.Liked,
        UserRating = 80m,
        NormalizedTagsCsv = "Great acting",
        Year = 2024
    };
    dbContext.Movies.Add(movie);
    await dbContext.SaveChangesAsync();

    var settingsPath = Path.Combine(Path.GetTempPath(), $"mymoviedb-test-settings-{Guid.NewGuid():N}.json");
    var preferences = new AppUserPreferencesService(new AppStorageOptions
    {
        DataHomePath = Path.GetTempPath(),
        DatabasePath = "memory",
        SeedPath = "seed",
        SettingsPath = settingsPath
    });

    var controller = new MoviesController(
        dbContext,
        new FakeMetadataService(),
        new MovieUpsertService(dbContext),
        new PersonalMatchService(new DeterministicRecommendationEngine(dbContext, new RecommendationFeatureExtractor(new PlotKeywordExtractor()), new RecommendationExplainer(), preferences)),
        new MetadataBackfillService(dbContext, new FakeMetadataService()),
        preferences);
    controller.ControllerContext = new ControllerContext
    {
        HttpContext = new DefaultHttpContext()
    };
    controller.ControllerContext.HttpContext.Request.Headers["X-Requested-With"] = "XMLHttpRequest";

    var result = await controller.ToggleWatched(movie.Id, null, CancellationToken.None);
    if (result is not JsonResult)
    {
        throw new Exception("Expected AJAX-style watched toggle result.");
    }

    var updated = await dbContext.Movies.SingleAsync(x => x.Id == movie.Id);
    if (updated.WatchedStatus != WatchedStatus.NotWatched)
    {
        throw new Exception("Expected toggle to move movie to NotWatched.");
    }

    if (updated.UserRating.HasValue || updated.UserGrade.HasValue || updated.PrimaryVerdict.HasValue)
    {
        throw new Exception("Expected toggle-to-unwatched to clear rating and grade fields.");
    }
}

static async Task AssertCompleteWatchRequiresThreeTagsAsync()
{
    await using var connection = new SqliteConnection("Data Source=:memory:");
    await connection.OpenAsync();

    var options = new DbContextOptionsBuilder<AppDbContext>()
        .UseSqlite(connection)
        .Options;

    await using var dbContext = new AppDbContext(options);
    await dbContext.Database.EnsureCreatedAsync();

    var movie = new Movie
    {
        Title = "Needs Three Tags",
        MediaType = MediaType.Movie,
        WatchedStatus = WatchedStatus.NotWatched,
        Year = 2024
    };
    dbContext.Movies.Add(movie);
    await dbContext.SaveChangesAsync();

    var settingsPath = Path.Combine(Path.GetTempPath(), $"mymoviedb-test-settings-{Guid.NewGuid():N}.json");
    var preferences = new AppUserPreferencesService(new AppStorageOptions
    {
        DataHomePath = Path.GetTempPath(),
        DatabasePath = "memory",
        SeedPath = "seed",
        SettingsPath = settingsPath
    });

    var controller = new MoviesController(
        dbContext,
        new FakeMetadataService(),
        new MovieUpsertService(dbContext),
        new PersonalMatchService(new DeterministicRecommendationEngine(dbContext, new RecommendationFeatureExtractor(new PlotKeywordExtractor()), new RecommendationExplainer(), preferences)),
        new MetadataBackfillService(dbContext, new FakeMetadataService()),
        preferences);
    controller.ControllerContext = new ControllerContext
    {
        HttpContext = new DefaultHttpContext()
    };
    controller.ControllerContext.HttpContext.Request.Headers["X-Requested-With"] = "XMLHttpRequest";

    var result = await controller.CompleteWatch(
        movie.Id,
        PersonalVerdict.Liked,
        ["Great acting", "Original idea"],
        null,
        CancellationToken.None);

    if (result is not JsonResult)
    {
        throw new Exception("Expected AJAX-style complete watch result.");
    }

    var updated = await dbContext.Movies.SingleAsync(x => x.Id == movie.Id);
    if (!updated.NeedsTagReview)
    {
        throw new Exception("Expected watched movies with fewer than three tags to stay in review.");
    }
}

static async Task AssertCompleteWatchWritesAppEventAsync()
{
    await using var connection = new SqliteConnection("Data Source=:memory:");
    await connection.OpenAsync();

    var options = new DbContextOptionsBuilder<AppDbContext>()
        .UseSqlite(connection)
        .Options;

    await using var dbContext = new AppDbContext(options);
    var migrator = new DatabaseSchemaMigrator();
    await migrator.MigrateAsync(dbContext);

    var movie = new Movie
    {
        Title = "Logged Watch",
        MediaType = MediaType.Movie,
        WatchedStatus = WatchedStatus.NotWatched,
        Year = 2024
    };
    dbContext.Movies.Add(movie);
    await dbContext.SaveChangesAsync();

    var preferences = CreatePreferencesService();
    var controller = new MoviesController(
        dbContext,
        new FakeMetadataService(),
        new MovieUpsertService(dbContext),
        new PersonalMatchService(new DeterministicRecommendationEngine(dbContext, new RecommendationFeatureExtractor(new PlotKeywordExtractor()), new RecommendationExplainer(), preferences)),
        new MetadataBackfillService(dbContext, new FakeMetadataService()),
        preferences);
    controller.ControllerContext = new ControllerContext
    {
        HttpContext = new DefaultHttpContext()
    };
    controller.ControllerContext.HttpContext.Request.Headers["X-Requested-With"] = "XMLHttpRequest";

    await controller.CompleteWatch(
        movie.Id,
        PersonalVerdict.Liked,
        ["Great acting", "Original idea", "Great twist"],
        null,
        CancellationToken.None);

    var eventCount = await GetAppEventCountAsync(connection, "Movie.CompleteWatch");
    if (eventCount != 1)
    {
        throw new Exception($"Expected one Movie.CompleteWatch app event, found {eventCount}.");
    }

    var summary = await GetLatestAppEventSummaryAsync(connection, "Movie.CompleteWatch");
    if (!summary.Contains("Logged Watch", StringComparison.Ordinal))
    {
        throw new Exception($"Expected watch event summary to include the movie title, found '{summary}'.");
    }
}

static async Task AssertCompleteWatchShowsImmediateMismatchSuggestionAsync()
{
    await using var connection = new SqliteConnection("Data Source=:memory:");
    await connection.OpenAsync();

    var options = new DbContextOptionsBuilder<AppDbContext>()
        .UseSqlite(connection)
        .Options;

    await using var dbContext = new AppDbContext(options);
    await dbContext.Database.EnsureCreatedAsync();

    dbContext.Movies.AddRange(
        new Movie
        {
            Title = "Sci-Fi Anchor",
            MediaType = MediaType.Movie,
            WatchedStatus = WatchedStatus.Watched,
            UserGrade = UserGrade.Loved,
            PrimaryVerdict = PersonalVerdict.Loved,
            UserRating = 95m,
            GenresCsv = "Sci-Fi",
            NormalizedTagsCsv = "Epic scale, Great worldbuilding, Incredible visuals",
            Overview = "A giant science fiction rescue mission on a grand scale.",
            Year = 2023,
            RuntimeMinutes = 173
        },
        new Movie
        {
            Title = "Fantasy Surprise",
            MediaType = MediaType.Movie,
            WatchedStatus = WatchedStatus.NotWatched,
            GenresCsv = "Fantasy",
            Overview = "Afterlife guides escort a hero through judgment and mythic trials.",
            Year = 2017,
            RuntimeMinutes = 139
        });

    await dbContext.SaveChangesAsync();

    var preferences = CreatePreferencesService();
    var controller = CreateMoviesController(dbContext, preferences);

    var result = await controller.CompleteWatch(
        await dbContext.Movies.Where(x => x.Title == "Fantasy Surprise").Select(x => x.Id).SingleAsync(),
        PersonalVerdict.Loved,
        ["Great acting", "Original idea"],
        null,
        CancellationToken.None);

    var payload = ExpectJsonResult(result);
    var suggestion = payload["mismatchSuggestion"]?.AsObject() ?? throw new Exception("Expected an immediate mismatch suggestion payload.");
    if (!string.Equals(suggestion["kind"]?.GetValue<string>(), "genre", StringComparison.Ordinal) ||
        !string.Equals(suggestion["value"]?.GetValue<string>(), "Fantasy", StringComparison.Ordinal) ||
        !string.Equals(suggestion["direction"]?.GetValue<string>(), "more", StringComparison.Ordinal))
    {
        throw new Exception($"Expected a positive fantasy suggestion, found '{suggestion.ToJsonString()}'.");
    }
}

static async Task AssertCompleteWatchAccumulatesMismatchMarksAsync()
{
    await using var connection = new SqliteConnection("Data Source=:memory:");
    await connection.OpenAsync();

    var options = new DbContextOptionsBuilder<AppDbContext>()
        .UseSqlite(connection)
        .Options;

    await using var dbContext = new AppDbContext(options);
    await dbContext.Database.EnsureCreatedAsync();

    dbContext.Movies.Add(new Movie
    {
        Title = "Sci-Fi Anchor",
        MediaType = MediaType.Movie,
        WatchedStatus = WatchedStatus.Watched,
        UserGrade = UserGrade.Loved,
        PrimaryVerdict = PersonalVerdict.Loved,
        UserRating = 95m,
        GenresCsv = "Sci-Fi",
        NormalizedTagsCsv = "Epic scale, Great worldbuilding, Incredible visuals",
        Overview = "A giant science fiction rescue mission on a grand scale.",
        Year = 2023,
        RuntimeMinutes = 173
    });

    for (var index = 1; index <= 3; index++)
    {
        dbContext.Movies.Add(new Movie
        {
            Title = $"Fantasy Maybe {index}",
            MediaType = MediaType.Movie,
            WatchedStatus = WatchedStatus.NotWatched,
            GenresCsv = "Fantasy",
            Overview = "Afterlife guides escort a hero through judgment and mythic trials.",
            Year = 2017 + index
        });
    }

    await dbContext.SaveChangesAsync();

    var preferences = CreatePreferencesService();
    var controller = CreateMoviesController(dbContext, preferences);
    var ids = await dbContext.Movies
        .Where(x => x.Title.StartsWith("Fantasy Maybe"))
        .OrderBy(x => x.Title)
        .Select(x => x.Id)
        .ToListAsync();

    for (var index = 0; index < ids.Count; index++)
    {
        var result = await controller.CompleteWatch(
            ids[index],
            PersonalVerdict.Okay,
            ["Great acting", "Original idea"],
            null,
            CancellationToken.None);

        var payload = ExpectJsonResult(result);
        var suggestion = payload["mismatchSuggestion"];
        if (index < 2 && suggestion is not null)
        {
            throw new Exception("Expected early medium mismatches to accumulate marks without prompting yet.");
        }

        if (index == 2 && suggestion is null)
        {
            throw new Exception("Expected the third medium mismatch to trigger a suggestion prompt.");
        }
    }
}

static async Task AssertDismissedMismatchSuggestionAppliesCooldownAsync()
{
    await using var connection = new SqliteConnection("Data Source=:memory:");
    await connection.OpenAsync();

    var options = new DbContextOptionsBuilder<AppDbContext>()
        .UseSqlite(connection)
        .Options;

    await using var dbContext = new AppDbContext(options);
    await dbContext.Database.EnsureCreatedAsync();

    dbContext.Movies.AddRange(
        new Movie
        {
            Title = "Sci-Fi Anchor",
            MediaType = MediaType.Movie,
            WatchedStatus = WatchedStatus.Watched,
            UserGrade = UserGrade.Loved,
            PrimaryVerdict = PersonalVerdict.Loved,
            UserRating = 95m,
            GenresCsv = "Sci-Fi",
            NormalizedTagsCsv = "Epic scale, Great worldbuilding, Incredible visuals",
            Overview = "A giant science fiction rescue mission on a grand scale.",
            Year = 2023
        },
        new Movie
        {
            Title = "Fantasy Dismiss One",
            MediaType = MediaType.Movie,
            WatchedStatus = WatchedStatus.NotWatched,
            GenresCsv = "Fantasy",
            Overview = "Afterlife guides escort a hero through judgment and mythic trials.",
            Year = 2018
        },
        new Movie
        {
            Title = "Fantasy Dismiss Two",
            MediaType = MediaType.Movie,
            WatchedStatus = WatchedStatus.NotWatched,
            GenresCsv = "Fantasy",
            Overview = "Afterlife guides escort a hero through judgment and mythic trials.",
            Year = 2019
        });

    await dbContext.SaveChangesAsync();

    var preferences = CreatePreferencesService();
    preferences.Save(new AppUserPreferences
    {
        CompletedRatingCount = 0,
        MismatchFactors =
        [
            new MismatchFactorState
            {
                Key = "genre:fantasy",
                Kind = "genre",
                Value = "Fantasy",
                Label = "Fantasy",
                PositiveMarks = 2
            }
        ]
    });

    var controller = CreateMoviesController(dbContext, preferences);

    var first = await controller.CompleteWatch(
        await dbContext.Movies.Where(x => x.Title == "Fantasy Dismiss One").Select(x => x.Id).SingleAsync(),
        PersonalVerdict.Okay,
        ["Great acting", "Original idea"],
        null,
        CancellationToken.None);
    var firstPayload = ExpectJsonResult(first);
    if (firstPayload["mismatchSuggestion"] is null)
    {
        throw new Exception("Expected accumulated fantasy mismatch marks to trigger a suggestion before dismissal.");
    }

    var dismissResult = await controller.ApplyMismatchPreferenceSuggestion(
        "genre",
        "Fantasy",
        "Fantasy",
        "more",
        "dismiss",
        CancellationToken.None);
    var dismissPayload = ExpectJsonResult(dismissResult);
    if (!dismissPayload["success"]!.GetValue<bool>())
    {
        throw new Exception("Expected mismatch dismissal to succeed.");
    }

    var second = await controller.CompleteWatch(
        await dbContext.Movies.Where(x => x.Title == "Fantasy Dismiss Two").Select(x => x.Id).SingleAsync(),
        PersonalVerdict.Okay,
        ["Great acting", "Original idea"],
        null,
        CancellationToken.None);
    var secondPayload = ExpectJsonResult(second);
    if (secondPayload["mismatchSuggestion"] is not null)
    {
        throw new Exception("Expected dismissed mismatch factor to stay on cooldown for the next similar mismatch.");
    }
}

static async Task AssertAcceptingMismatchSuggestionPersistsPreferenceAsync()
{
    await using var connection = new SqliteConnection("Data Source=:memory:");
    await connection.OpenAsync();

    var options = new DbContextOptionsBuilder<AppDbContext>()
        .UseSqlite(connection)
        .Options;

    await using var dbContext = new AppDbContext(options);
    await dbContext.Database.EnsureCreatedAsync();

    dbContext.Movies.Add(new Movie
    {
        Title = "Simple Candidate",
        MediaType = MediaType.Movie,
        WatchedStatus = WatchedStatus.NotWatched,
        GenresCsv = "Fantasy",
        Year = 2024
    });
    await dbContext.SaveChangesAsync();

    var preferences = CreatePreferencesService();
    var controller = CreateMoviesController(dbContext, preferences);

    var result = await controller.ApplyMismatchPreferenceSuggestion(
        "genre",
        "Fantasy",
        "Fantasy",
        "more",
        "accept",
        CancellationToken.None);
    var payload = ExpectJsonResult(result);
    if (!payload["success"]!.GetValue<bool>())
    {
        throw new Exception("Expected accepting mismatch preference to succeed.");
    }

    var saved = preferences.Get();
    if (!saved.GenreAdjustments.TryGetValue("Fantasy", out var value) || value <= 0m)
    {
        throw new Exception("Expected accepted fantasy mismatch suggestion to persist a positive genre adjustment.");
    }
}

static async Task AssertDismissAndRestoreWriteAppEventsAsync()
{
    await using var connection = new SqliteConnection("Data Source=:memory:");
    await connection.OpenAsync();

    var options = new DbContextOptionsBuilder<AppDbContext>()
        .UseSqlite(connection)
        .Options;

    await using var dbContext = new AppDbContext(options);
    var migrator = new DatabaseSchemaMigrator();
    await migrator.MigrateAsync(dbContext);

    var movie = new Movie
    {
        Title = "Logged Dismiss",
        MediaType = MediaType.Movie,
        WatchedStatus = WatchedStatus.NotWatched,
        Year = 2024
    };
    dbContext.Movies.Add(movie);
    await dbContext.SaveChangesAsync();

    var preferences = CreatePreferencesService();
    var controller = new MoviesController(
        dbContext,
        new FakeMetadataService(),
        new MovieUpsertService(dbContext),
        new PersonalMatchService(new DeterministicRecommendationEngine(dbContext, new RecommendationFeatureExtractor(new PlotKeywordExtractor()), new RecommendationExplainer(), preferences)),
        new MetadataBackfillService(dbContext, new FakeMetadataService()),
        preferences);
    controller.ControllerContext = new ControllerContext
    {
        HttpContext = new DefaultHttpContext()
    };
    controller.TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(
        controller.HttpContext,
        new FakeTempDataProvider());

    await controller.Dismiss(movie.Id, ["Too dark"], null, CancellationToken.None);
    await controller.Restore(movie.Id, null, CancellationToken.None);

    var dismissCount = await GetAppEventCountAsync(connection, "Movie.Dismiss");
    var restoreCount = await GetAppEventCountAsync(connection, "Movie.Restore");
    if (dismissCount != 1 || restoreCount != 1)
    {
        throw new Exception($"Expected dismiss/restore app events, found dismiss={dismissCount}, restore={restoreCount}.");
    }
}

static async Task AssertImportUploadWritesAppEventAsync()
{
    await using var connection = new SqliteConnection("Data Source=:memory:");
    await connection.OpenAsync();

    var options = new DbContextOptionsBuilder<AppDbContext>()
        .UseSqlite(connection)
        .Options;

    await using var dbContext = new AppDbContext(options);
    var migrator = new DatabaseSchemaMigrator();
    await migrator.MigrateAsync(dbContext);

    var preferences = CreatePreferencesService();
    var controller = new ImportController(
        dbContext,
        new DocxMovieImportService(),
        new JsonMovieImportService(),
        new CsvMovieDeltaImportService(),
        new MovieUpsertService(dbContext),
        new PersonalMatchService(new DeterministicRecommendationEngine(dbContext, new RecommendationFeatureExtractor(new PlotKeywordExtractor()), new RecommendationExplainer(), preferences)));
    controller.ControllerContext = new ControllerContext
    {
        HttpContext = new DefaultHttpContext()
    };
    controller.TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(
        controller.HttpContext,
        new FakeTempDataProvider());

    const string csv = "Title,Year,LegacyUserScore,UserGrade,NewTagsCsv,Action,ReviewStatus,Source,OriginalTagsCsv,DroppedLegacyTagsCsv,Notes\n\"Import Event\",2024,80,Liked,\"Great acting, Original idea\",UpdateExisting,ReadyForUpdate,CSV,,,note";
    var bytes = System.Text.Encoding.UTF8.GetBytes(csv);
    var formFile = new FormFile(new MemoryStream(bytes), 0, bytes.Length, "uploadFile", "sample.csv");

    await controller.Upload(formFile, CancellationToken.None);

    var eventCount = await GetAppEventCountAsync(connection, "Import.Upload");
    if (eventCount != 1)
    {
        throw new Exception($"Expected one Import.Upload app event, found {eventCount}.");
    }

    var summary = await GetLatestAppEventSummaryAsync(connection, "Import.Upload");
    if (!summary.Contains("sample.csv", StringComparison.Ordinal))
    {
        throw new Exception($"Expected import event summary to include the upload filename, found '{summary}'.");
    }
}

static async Task<int> GetAppEventCountAsync(SqliteConnection connection, string eventType)
{
    await using var command = connection.CreateCommand();
    command.CommandText = "SELECT COUNT(*) FROM AppEvents WHERE EventType = $eventType;";
    command.Parameters.AddWithValue("$eventType", eventType);
    var result = await command.ExecuteScalarAsync();
    return Convert.ToInt32(result, System.Globalization.CultureInfo.InvariantCulture);
}

static async Task<string> GetLatestAppEventSummaryAsync(SqliteConnection connection, string eventType)
{
    await using var command = connection.CreateCommand();
    command.CommandText = "SELECT Summary FROM AppEvents WHERE EventType = $eventType ORDER BY OccurredUtc DESC, Id DESC LIMIT 1;";
    command.Parameters.AddWithValue("$eventType", eventType);
    var result = await command.ExecuteScalarAsync();
    return result?.ToString() ?? string.Empty;
}

static async Task AssertRestoreFromExportCsvAsync()
{
    const string csv = "Title,Year,Genres,Watched,Dismissed,UserScore,PredictedScore,TasteFit,PredictedLabel,PredictedReason,IMDb,ReviewBadge,DismissedReasons,ReasonTags\n\"Watched Film\",2024,Thriller,Watched,No,95,80,82,Likely,Why,7.0,,,\"Great acting, Original idea\"\n\"Unwatched Film\",2025,Horror,NotWatched,No,,70,72,Maybe,Why,6.5,,,\"\"";
    await using var connection = new SqliteConnection("Data Source=:memory:");
    await connection.OpenAsync();

    var options = new DbContextOptionsBuilder<AppDbContext>()
        .UseSqlite(connection)
        .Options;

    await using var dbContext = new AppDbContext(options);
    await dbContext.Database.EnsureCreatedAsync();
    dbContext.Movies.AddRange(
        new Movie { Title = "Watched Film", Year = 2024, MediaType = MediaType.Movie, WatchedStatus = WatchedStatus.Watched, UserRating = 95m, UserGrade = UserGrade.Loved, PrimaryVerdict = PersonalVerdict.Loved },
        new Movie { Title = "Unwatched Film", Year = 2025, MediaType = MediaType.Movie, WatchedStatus = WatchedStatus.Watched, UserRating = 70m, UserGrade = UserGrade.Liked, PrimaryVerdict = PersonalVerdict.Liked });
    await dbContext.SaveChangesAsync();

    await using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(csv));
    var parser = new ExportCsvRestoreService();
    var rows = await parser.ParseExportAsync(stream);
    foreach (var row in rows)
    {
        await parser.ApplyAsync(dbContext, row);
    }

    var watched = await dbContext.Movies.SingleAsync(x => x.Title == "Watched Film");
    var unwatched = await dbContext.Movies.SingleAsync(x => x.Title == "Unwatched Film");
    if (watched.WatchedStatus != WatchedStatus.Watched || unwatched.WatchedStatus != WatchedStatus.NotWatched)
    {
        throw new Exception("Expected export restore to recover watched and unwatched states.");
    }
}

static AppUserPreferences CreatePreferencesWithImportantTags(params string[] tags)
{
    var preferences = AppUserPreferences.CreateDefault();
    var cleaned = tags.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();

    var importantTagsProperty = typeof(AppUserPreferences).GetProperty("ImportantTags");
    if (importantTagsProperty is not null && importantTagsProperty.CanWrite)
    {
        importantTagsProperty.SetValue(preferences, cleaned);
        return preferences;
    }

    for (var index = 0; index < Math.Min(cleaned.Count, 4); index++)
    {
        var property = typeof(AppUserPreferences).GetProperty($"ImportantTag{index + 1}");
        property?.SetValue(preferences, cleaned[index]);
    }

    return preferences;
}

static MoviesController CreateMoviesController(AppDbContext dbContext, AppUserPreferencesService preferences)
{
    var controller = new MoviesController(
        dbContext,
        new FakeMetadataService(),
        new MovieUpsertService(dbContext),
        new PersonalMatchService(new DeterministicRecommendationEngine(dbContext, new RecommendationFeatureExtractor(new PlotKeywordExtractor()), new RecommendationExplainer(), preferences)),
        new MetadataBackfillService(dbContext, new FakeMetadataService()),
        preferences);
    controller.ControllerContext = new ControllerContext
    {
        HttpContext = new DefaultHttpContext()
    };
    controller.ControllerContext.HttpContext.Request.Headers["X-Requested-With"] = "XMLHttpRequest";
    return controller;
}

static JsonObject ExpectJsonResult(IActionResult result)
{
    if (result is not JsonResult json)
    {
        throw new Exception("Expected JSON action result.");
    }

    return JsonNode.Parse(JsonSerializer.Serialize(json.Value))?.AsObject()
        ?? throw new Exception("Expected JSON payload.");
}

static AppUserPreferencesService CreatePreferencesService()
{
    var settingsPath = Path.Combine(Path.GetTempPath(), $"mymoviedb-test-settings-{Guid.NewGuid():N}.json");
    return new AppUserPreferencesService(new AppStorageOptions
    {
        DataHomePath = Path.GetTempPath(),
        DatabasePath = "memory",
        SeedPath = "seed",
        SettingsPath = settingsPath
    });
}

sealed class FakeMetadataService : IMovieMetadataService
{
    public Task<MetadataLookupResult> LookupByTitleAsync(string title, int? year, CancellationToken cancellationToken = default)
        => Task.FromResult(new MetadataLookupResult { Success = false, ErrorMessage = "Not used in tests." });

    public Task<MetadataLookupResult> LookupByImdbIdAsync(string imdbId, CancellationToken cancellationToken = default)
        => Task.FromResult(new MetadataLookupResult { Success = false, ErrorMessage = "Not used in tests." });

    public Task<IReadOnlyList<MetadataSearchCandidate>> SearchCandidatesAsync(string title, int? year, int take = 5, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<MetadataSearchCandidate>>([]);
}

sealed class FakeTempDataProvider : Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider
{
    public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>(StringComparer.Ordinal);

    public void SaveTempData(HttpContext context, IDictionary<string, object> values)
    {
    }
}
