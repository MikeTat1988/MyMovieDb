# LocalMovieVault Quick Map

Purpose: fast, low-token orientation for future fixes without rescanning the whole repo.

## Runtime Path

- Active layout: `src/LocalMovieVault.Web/Views/Shared/_AppLayout.cshtml`
- Active stylesheet loaded by runtime: `src/LocalMovieVault.Web/wwwroot/css/site.css`
- Active frontend logic loaded by runtime: `src/LocalMovieVault.Web/wwwroot/js/app-main.js`
- Active home views:
  - Best match: `src/LocalMovieVault.Web/Views/Home/BestMatch.cshtml`
  - Surprise: `src/LocalMovieVault.Web/Views/Home/Surprise.cshtml`
- Active library/details views:
  - Library: `src/LocalMovieVault.Web/Views/Movies/Library.cshtml`
  - Details: `src/LocalMovieVault.Web/Views/Movies/MovieDetails.cshtml`
- Shared active card/modal partials:
  - `src/LocalMovieVault.Web/Views/Shared/_DiscoveryCard.cshtml`
  - `src/LocalMovieVault.Web/Views/Shared/_WatchFeedbackModal.cshtml`

## Main Controllers

- `Controllers/HomeController.cs`
  - `Index`: Best Match page
  - `Surprise`: random eligible pick
- `Controllers/MoviesController.cs`
  - `Index`: library sections (`not-watched`, `watched`, `review`, `dismissed`)
  - `Details`: movie details page
  - `CompleteWatch`: saves watched verdict/tags
  - `ToggleWatched`: mark watched/unwatched
  - `Dismiss` / `Restore`
  - `SetRating` / `SetTaste`: legacy quick-update endpoints, no longer the main UI path
- `Controllers/ImportController.cs`
  - import/seed flows
- `Controllers/SettingsController.cs`
  - settings, preview, export

## Core Rules

- Review logic lives in `Helpers/MovieStateHelper.cs`
  - Watched titles go to review if grade missing, `NeedsTagReview` is true, or tag count is below the minimum
  - Unwatched titles use the display match score against the dismiss threshold
- Match score shown to users comes from `Helpers/RecommendationViewHelper.GetDisplayMatchScore`
  - prefers `PredictedScore`
  - falls back to `PersonalMatchScore`
- Tag requirements live in `Helpers/RecommendationViewHelper.cs`
  - standard watched minimum: 3 tags
  - couldn't-finish minimum: 0 tags
  - tag definitions also live here, including genre-specific tags

## Recommendation Flow

- Engine: `Services/Recommendations/DeterministicRecommendationEngine.cs`
- Feature extraction: `Services/Recommendations/RecommendationFeatureExtractor.cs`
- Keyword/tag catalog: `Services/Recommendations/RecommendationCatalog.cs`
- User-facing match/review decisions should use `PredictedScore` first
- `PersonalMatchScore` still exists as an internal broad-fit score and may be useful for diagnostics

## Frontend Interaction Model

- Cards carry movie state through `data-*` attributes in `_DiscoveryCard.cshtml`
- `app-main.js` owns:
  - watched modal open/prefill
  - genre-specific tag visibility
  - mark-unwatched flow
  - AJAX submit for watched and dismiss flows
  - settings tab/preview helpers
- Current intended card UX:
  - `Review` opens the modal for watched/review work
  - `Mark watched` opens the modal for unwatched titles
  - `Mark unwatched` is a separate explicit action

## Data + Storage

- EF model: `Models/Movie.cs`
- DbContext: `Data/AppDbContext.cs`
- Real user data location is outside repo, under Documents:
  - database: `MyMovieDB/localmovievault.db`
  - settings: `MyMovieDB/mymoviedb.settings.json`
- Repo `App_Data` is not the primary live database once bootstrapped

## Testing + Verification

- Smoke tests entrypoint: `tests/LocalMovieVault.Web.Tests/Program.cs`
- Important current smoke coverage:
  - display score preference
  - review badge thresholding
  - genre-aware tag grouping
  - toggle watched clears watched fields
  - watched flow with fewer than 3 tags stays in review
- Standard verification:
  - `dotnet run --project tests/LocalMovieVault.Web.Tests/LocalMovieVault.Web.Tests.csproj`
  - `dotnet build src/LocalMovieVault.Web/LocalMovieVault.Web.csproj`
  - `build-release.bat` when shipping

## Current Cleanup Notes

- The runtime now points to the new single frontend entrypoint: `wwwroot/js/app-main.js`
- Some older files may still physically exist even if unreferenced; before reusing any legacy view/script, check `_AppLayout.cshtml` and the controller `return View(...)` calls first
