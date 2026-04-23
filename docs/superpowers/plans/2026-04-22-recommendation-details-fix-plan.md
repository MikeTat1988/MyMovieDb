# Recommendation Details Fix Plan

## Current Status

- Phase 1 implementation: complete
- Phase 1 build verification: passed
- Phase 1 smoke verification: passed
- Phase 2 implementation: complete
- Phase 2 build verification: passed
- Phase 2 smoke verification: passed
- Phase 3 implementation: complete
- Phase 3 build verification: passed
- Phase 3 smoke verification: passed
- All implementation phases: complete
- Packaging status: no zip/package produced yet; keep packaging for the final post-Phase 3 ship step

## Summary

- Fix contradictory recommendation explanations so low scores explain why the movie is a weak fit.
- Replace the misleading decision table with a single-reference comparison section that names one app-chosen movie once in the heading.
- Make the details UI mobile-first, with a standalone HTML review mock before touching the production page.
- Add a compact `Fit` badge in the hero so the page shows `Match`, `IMDb`, and an easy-to-read fit label together.
- Recalibrate score distribution so `95-96+` is rare and meaningful.
- Review whether current ratings are helping and reduce damage from incomplete `NeedsTagReview` training data.
- Add guided mismatch-based preference suggestions, but only after the core prediction system is more trustworthy.

## Confirmed Problems

- `Along with the Gods` currently stores `PredictedScore = 26.8` and a persuasive `PredictedReason`, so the explanation contradicts the score.
- The details page uses a weakest-liked fallback that can show a movie like `Wandering Earth 2 (1.5)` as if it were a meaningful benchmark for every parameter.
- The current scoring compresses too many movies into the top band and overuses `96`.
- The live watched/rated set has `36` rated titles, but only `18` completed reviews. The other `18` still need tags.
- The watched history is strongly positive-heavy, which weakens reliable negative evidence and broad genre-risk claims.

## Phase 1: Recommendation Truthfulness

### Goal

Make the recommendation engine and explanation output trustworthy before redesigning the details page.

### Scope

- Fix score-aware explanation logic in `RecommendationExplainer`
- Exclude `NeedsTagReview` watched items from core training and reference selection
- Rework high-end score calibration so top-band scores are rarer and more meaningful
- Add diagnostics/reporting for rating usefulness and mismatch visibility

### Implementation

- Update `RecommendationExplainer` so summary tone depends on the final calibrated score band.
- For low scores, lead with blockers first and never use persuasive phrases such as `Likely to work`.
- Show genre risk only when it is supported by reliable completed-review evidence.
- Do not present tiny similarity anchors as persuasive support.
- In `DeterministicRecommendationEngine`, exclude watched items with `NeedsTagReview = true` from the primary taste profile and from similarity/explanation anchor selection.
- Treat these items as pending feedback, not reliable recommendation evidence.
- Rework `ApplyCalibratedScores` so percentile rank is only a mild stabilizer.
- Make `95+` require strong evidence and strong confidence, not just rank.
- Audit important-tag bonuses so they cannot easily push weak candidates into the top band.
- Add reporting that distinguishes complete watched ratings, incomplete watched ratings, positive vs negative distribution, and high-impact incomplete ratings.
- Add a high-impact review queue for watched titles still needing tags.
- Capture large prediction mismatches as a first-class diagnostic signal so we can review where the model misunderstood the user.
- Use that mismatch signal for reporting and future tuning, but do not silently rewrite user preferences or settings.

### Files

- `C:\Dev\MovieDb\src\LocalMovieVault.Web\Services\Recommendations\DeterministicRecommendationEngine.cs`
- `C:\Dev\MovieDb\src\LocalMovieVault.Web\Services\Recommendations\RecommendationExplainer.cs`
- `C:\Dev\MovieDb\src\LocalMovieVault.Web\Services\Recommendations\RecommendationModels.cs`
- `C:\Dev\MovieDb\src\LocalMovieVault.Web\Controllers\MoviesController.cs`
- `C:\Dev\MovieDb\tests\LocalMovieVault.Web.Tests\Program.cs`

### Tests

- Add explainer tests proving low-score movies never say `Likely to work`.
- Add tests proving `NeedsTagReview` items are excluded from core training/reference selection.
- Add tests proving score distribution uses more of the range and top-end scores are rarer.
- Add tests for mismatch diagnostics without yet adding mismatch-guidance prompts.

### Completion Marker

Phase 1 is complete when all of the following are true:

- low-score explanations are honest
- `NeedsTagReview` items are no longer trusted as core taste anchors
- score spread is visibly improved
- tests for the new engine/explainer behavior pass
- no UI redesign work is required to validate the result

**Next run should start with Phase 2 only after this marker is reached.**

## Phase 2: Details Page Redesign

### Goal

Ship the new full-page details UI using real in-app data and the improved Phase 1 outputs.

### Scope

- Replace the current decision table with a single-reference comparison section
- Implement the full-page poster-first layout from the approved mock
- Add the `Fit` badge and new hero-slot behavior
- Rename `Short summary` to `Rating explanation`
- Move `Edit` and `Delete` into the bottom action row

### Implementation

- Remove the current weakest-liked benchmark behavior from `MovieDetailsViewModel`.
- Introduce a selected reference-movie summary in the recommendation context/view-model.
- Show the reference movie name once in the section heading, for example `Comparison vs Wandering Earth 2`.
- Keep rows focused on parameter values only for readability.
- If no valid comparison movie exists, switch the section to a direct evidence view such as `Why this score`.
- Keep the top hero compact and poster-first.
- Use a consistent right-side hero slot that behaves like this:
  - unwatched: show `Review` button
  - watched + under review: show `Review` button
  - watched + complete: show the user's final rating label in the same UI space
- Drive the page from real in-app fields instead of mock-only content:
  - poster from `PosterUrl`
  - title/year/genres/runtime/overview from `Movie`
  - match from `PredictedScore` with fallback to `PersonalMatchScore`
  - IMDb from `ImdbRating`
  - review-state badge from existing review logic
  - user rating/grade from `UserRating` and `UserGrade`
  - rating explanation from `PredictedReason`, upgraded to the new score-aware explainer output
  - comparison section from `RecommendationContextJson`
- Use a centered comparison card with a centered heading, short subtitle, and one reference chip naming the chosen movie.
- Use two presentations of the same data:
  - desktop/tablet: centered three-column comparison table
  - phone: stacked comparison cards with `This movie` and `Reference`
- Avoid horizontal overflow and repeated long labels.
- Rename `Short summary` to `Rating explanation`.
- Move `Edit` and `Delete` into a bottom action row; `Edit` should open the same review flow instead of a separate full-edit destination for this page.

### Files

- `C:\Dev\MovieDb\src\LocalMovieVault.Web\ViewModels\MovieDetailsViewModel.cs`
- `C:\Dev\MovieDb\src\LocalMovieVault.Web\Views\Movies\MovieDetails.cshtml`
- `C:\Dev\MovieDb\src\LocalMovieVault.Web\wwwroot\css\site.css`
- `C:\Dev\MovieDb\src\LocalMovieVault.Web\wwwroot\js\app-main.js`
- `C:\Dev\MovieDb\tests\LocalMovieVault.Web.Tests\Program.cs`

### Tests

- Add responsive/details view-model assertions for the new comparison section.
- Add tests covering hero-slot states for:
  - unwatched movies
  - watched movies still under review
  - watched movies with completed review
- Add tests proving the details page binds to real movie/context fields and handles missing poster/reference data gracefully.

### Completion Marker

Phase 2 is complete when all of the following are true:

- the details page matches the approved UI direction
- the page uses real app fields rather than mock-only data
- the hero-slot button/rating behavior is correct
- the comparison section is mobile-safe
- the updated details page tests pass

**Next run should start with Phase 3 only after this marker is reached.**

## Phase 3: Guided Mismatch Suggestions

### Goal

Turn repeated prediction misses into explicit, rate-limited preference suggestions that the user can accept or dismiss.

### Scope

- Add mismatch marks, cooldowns, and factor-based suggestion prompts
- Keep this system conservative and user-confirmed
- Avoid broad automatic assumptions such as country/language bias from thin evidence

### Implementation

- Treat mismatch as a user-confirmed tuning opportunity, not an automatic weight rewrite.
- Use two mismatch bands:
  - `>= 40`: ask immediately
  - `28-39.9`: add mismatch marks and ask only after repeated pattern
- Track mismatch marks per factor and direction:
  - positive surprise: user liked it more than expected
  - negative surprise: user liked it less than expected
- Score marks conservatively:
  - `>= 40` mismatch adds `2` marks
  - `28-39.9` mismatch adds `1` mark
  - ask when the same factor/direction reaches `3` marks
- Limit factor selection to the top 1-2 plausible causes behind the mismatch, preferring:
  - genre
  - director
  - tone/style tags
  - pacing/runtime-related tags
  - story/theme tags
- Only suggest country/language preferences when the pattern is repeated and well-supported; do not jump to broad assumptions from one or two movies.
- Suggested prompt behavior:
  - positive: `You liked this more than expected. Want more movies with [factor]?`
  - negative: `You liked this less than expected. Want fewer movies with [factor]?`
- If the user accepts:
  - apply a small explicit boost/penalty to the relevant preference or factor group
  - log it as a user-confirmed adjustment
- If the user dismisses:
  - do not repeat that same factor prompt immediately
  - apply a cooldown such as `14 days` or `5 future ratings`, whichever comes later
- After prompting, reset or reduce marks for that factor to avoid repeated nagging.
- Add diagnostics so Settings can show:
  - recent mismatch factors
  - accepted suggestions
  - dismissed suggestions
  - factors that are close to prompting

### Files

- `C:\Dev\MovieDb\src\LocalMovieVault.Web\Controllers\MoviesController.cs`
- `C:\Dev\MovieDb\src\LocalMovieVault.Web\Services\Recommendations\DeterministicRecommendationEngine.cs`
- `C:\Dev\MovieDb\src\LocalMovieVault.Web\Services\Recommendations\RecommendationModels.cs`
- `C:\Dev\MovieDb\src\LocalMovieVault.Web\Services\AppUserPreferencesService.cs`
- `C:\Dev\MovieDb\src\LocalMovieVault.Web\Views\Shared\_WatchFeedbackModal.cshtml`
- `C:\Dev\MovieDb\src\LocalMovieVault.Web\wwwroot\js\app-main.js`
- `C:\Dev\MovieDb\tests\LocalMovieVault.Web.Tests\Program.cs`

### Tests

- Add tests for mismatch-guidance behavior:
  - immediate prompt on `>= 40` mismatch
  - mark accumulation on `28-39.9` mismatch
  - prompt after repeated factor pattern
  - cooldown after dismiss
  - small explicit preference adjustment after accept

### Completion Marker

Phase 3 is complete when all of the following are true:

- mismatch prompts are helpful and rate-limited
- accepted prompts create explicit, reviewable preference adjustments
- dismissed prompts respect cooldowns
- tests prove the system does not spam or silently rewrite taste

**No later phase should assume this is done until this marker is reached.**

## Assumptions

- The UI should be reviewed visually before implementation.
- The reference movie may still be `Wandering Earth 2` in the mock, but shipped logic must choose it only when similarity is meaningful.
- Incomplete watched reviews are useful as user data, but not reliable enough to act as full recommendation anchors until review is complete.
- Best implementation quality comes from shipping these phases sequentially rather than as one giant run.
