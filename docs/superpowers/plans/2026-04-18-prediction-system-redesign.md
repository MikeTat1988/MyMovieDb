# Prediction System Redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Redesign the deterministic prediction system so match scores rank better, stay independent from user feedback, and surface clearly in the card UI alongside IMDb and post-review user scores.

**Architecture:** Keep the existing deterministic recommendation pipeline, but separate affinity, confidence, and calibrated display scoring. Preserve the current persisted result fields while updating the card UI to always show system match output and conditionally show the user's completed-review score as a separate metric.

**Tech Stack:** ASP.NET Core MVC, EF Core, Razor views, existing smoke-test harness in `tests/LocalMovieVault.Web.Tests`, CSS in `wwwroot/css/site.css`

---

### Task 1: Add Scoring Regression Tests

**Files:**
- Modify: `C:\Dev\MovieDb\tests\LocalMovieVault.Web.Tests\Program.cs`

- [ ] **Step 1: Write the failing tests**

Add smoke tests for:
- `Meh` being neutral instead of negative
- top matching candidates reaching a high display score without perfect tag overlap
- watched titles keeping `PredictedScore` independent from `UserRating`
- card markup showing Match always and User only after review completion

- [ ] **Step 2: Run the smoke test project to verify the new tests fail**

Run: `dotnet run --project C:\Dev\MovieDb\tests\LocalMovieVault.Web.Tests\LocalMovieVault.Web.Tests.csproj`

Expected: FAIL on the newly added recommendation and UI assertions.

- [ ] **Step 3: Keep the failing assertions focused**

Use in-memory SQLite tests that build tiny movie sets and inspect resulting `PredictedScore`, `PersonalMatchScore`, and view markup strings directly.

- [ ] **Step 4: Re-run the smoke tests until the failures are specifically about the missing redesign behavior**

Run: `dotnet run --project C:\Dev\MovieDb\tests\LocalMovieVault.Web.Tests\LocalMovieVault.Web.Tests.csproj`

Expected: FAIL with redesign-specific expectation messages, not setup errors.

### Task 2: Redesign the Recommendation Engine

**Files:**
- Modify: `C:\Dev\MovieDb\src\LocalMovieVault.Web\Services\Recommendations\RecommendationCatalog.cs`
- Modify: `C:\Dev\MovieDb\src\LocalMovieVault.Web\Services\Recommendations\DeterministicRecommendationEngine.cs`
- Modify: `C:\Dev\MovieDb\src\LocalMovieVault.Web\Services\Recommendations\RecommendationModels.cs`
- Modify: `C:\Dev\MovieDb\src\LocalMovieVault.Web\Services\Recommendations\RecommendationExplainer.cs`
- Modify: `C:\Dev\MovieDb\src\LocalMovieVault.Web\Services\Recommendations\RecommendationFeatureExtractor.cs`

- [ ] **Step 1: Change the rating evidence model**

Update `RecommendationCatalog.GetGradeWeight` inputs so:
- `Loved` remains strong positive
- `Liked` remains mild positive
- `Meh` becomes neutral
- `CouldntFinish` remains strong negative

- [ ] **Step 2: Add minimal model support for affinity/confidence diagnostics**

Extend the recommendation context/result model just enough to carry:
- affinity score
- confidence score
- optional compact diagnostics used for logging/explanations

- [ ] **Step 3: Implement the new score pipeline**

In `DeterministicRecommendationEngine`:
- compute raw affinity from existing feature matches
- compute confidence from metadata quality, quality confidence, and evidence counts
- calibrate `PredictedScore` from affinity rank/banding plus confidence moderation
- keep `PersonalMatchScore` as the affinity-oriented score
- preserve `PredictedLabel` and `PredictedReason` as model outputs only

- [ ] **Step 4: Add targeted logging**

Add concise recommendation logs where recalculation happens so failures can be diagnosed quickly, including:
- movie title/id
- affinity
- confidence
- predicted score
- top positive and negative factors

Keep logs compact and suitable for fast local troubleshooting.

- [ ] **Step 5: Make the explanation text reflect the redesigned output**

Adjust `RecommendationExplainer` so it can mention strong evidence vs limited evidence without changing the overall explanation format.

- [ ] **Step 6: Run the smoke tests to verify the engine tests pass**

Run: `dotnet run --project C:\Dev\MovieDb\tests\LocalMovieVault.Web.Tests\LocalMovieVault.Web.Tests.csproj`

Expected: previously failing recommendation tests now pass, UI tests may still fail.

### Task 3: Update Card UI for Match vs User Comparison

**Files:**
- Modify: `C:\Dev\MovieDb\src\LocalMovieVault.Web\Views\Shared\_DiscoveryCard.cshtml`
- Modify: `C:\Dev\MovieDb\src\LocalMovieVault.Web\wwwroot\css\site.css`
- Inspect if needed: `C:\Dev\MovieDb\src\LocalMovieVault.Web\Views\Movies\Library.cshtml`

- [ ] **Step 1: Update the view markup**

Render one aligned metric row that:
- always shows IMDb
- always shows Match from the system score
- shows User only when watched review is complete

Use labels that make the independence obvious and keep the card easy to scan.

- [ ] **Step 2: Refine the styles**

Update card styles so the metric row is:
- visually balanced
- aligned on desktop and mobile
- readable even when all three values appear
- consistent with the current movie-first visual language

- [ ] **Step 3: Add any small view-level safeguards**

Only show User when the movie has completed review evidence, not when it is still in review or missing final feedback.

- [ ] **Step 4: Run the smoke tests to verify the UI rules pass**

Run: `dotnet run --project C:\Dev\MovieDb\tests\LocalMovieVault.Web.Tests\LocalMovieVault.Web.Tests.csproj`

Expected: UI assertions pass along with the recommendation tests.

### Task 4: Full Verification

**Files:**
- Modify only if needed from prior tasks

- [ ] **Step 1: Run a clean project build**

Run: `dotnet build C:\Dev\MovieDb\src\LocalMovieVault.Web\LocalMovieVault.Web.csproj`

Expected: Build succeeds with 0 errors.

- [ ] **Step 2: Run the full smoke test suite again**

Run: `dotnet run --project C:\Dev\MovieDb\tests\LocalMovieVault.Web.Tests\LocalMovieVault.Web.Tests.csproj`

Expected: `All LocalMovieVault.Web smoke tests passed.`

- [ ] **Step 3: Package the shipped update**

Run: `C:\Dev\MovieDb\build-release.bat`

Expected: publish output lands in `build/LocalMovieVault.Web`, zip is produced, and copied to `G:\My Drive\MasterApp\Incoming`.

- [ ] **Step 4: Check workspace hygiene**

Confirm no disposable logs or ad-hoc output folders were left behind.
