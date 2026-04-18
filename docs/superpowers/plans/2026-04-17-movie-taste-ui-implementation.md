# Movie Taste UI And Scoring Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Clean the movie grid and make recommendation scores reflect the user's actual taste instead of over-reporting perfect matches.

**Architecture:** Keep the current ASP.NET MVC structure, but separate taste-fit scoring from recommendation scoring inside the deterministic engine. UI changes stay in the shared movie card and details views, while tests exercise the recommendation engine through a lightweight SQLite-backed test harness.

**Tech Stack:** ASP.NET Core MVC, Entity Framework Core SQLite, Razor views, console-style .NET test project

---

### Task 1: Lock down desired behavior with failing tests

**Files:**
- Modify: `tests/LocalMovieVault.Web.Tests/Program.cs`

- [ ] Add regression coverage for the new taste tags.
- [ ] Add a scoring scenario where a strong twist/original-story/cinematography candidate outranks a generic same-genre candidate.
- [ ] Add a scoring scenario where `PersonalMatchScore` and `PredictedScore` intentionally diverge.
- [ ] Run `dotnet run --project tests/LocalMovieVault.Web.Tests/LocalMovieVault.Web.Tests.csproj` and confirm failure before production edits.

### Task 2: Rebuild the deterministic scoring model

**Files:**
- Modify: `src/LocalMovieVault.Web/Services/Recommendations/DeterministicRecommendationEngine.cs`
- Modify: `src/LocalMovieVault.Web/Services/Recommendations/RecommendationCatalog.cs`
- Modify: `src/LocalMovieVault.Web/Services/Recommendations/RecommendationModels.cs`

- [ ] Split taste-fit and watch-priority calculations instead of writing the same number to both fields.
- [ ] Reduce the baseline and dampen generic metadata contributions.
- [ ] Increase the weight of direct user-taste evidence, especially the preferred ending/originality/cinematography signals.
- [ ] Keep explanation output coherent with the new scoring behavior.

### Task 3: Update taste vocabulary

**Files:**
- Modify: `src/LocalMovieVault.Web/Helpers/RecommendationViewHelper.cs`
- Modify: `src/LocalMovieVault.Web/Services/Recommendations/RecommendationCatalog.cs`

- [ ] Replace weaker generic labels where helpful.
- [ ] Ensure the tag list reflects the user's preferred signals.
- [ ] Keep hint matching aligned with the renamed tags.

### Task 4: Polish the library and details UI

**Files:**
- Modify: `src/LocalMovieVault.Web/Views/Shared/_MovieCard.cshtml`
- Modify: `src/LocalMovieVault.Web/Views/Movies/Details.cshtml`
- Modify: `src/LocalMovieVault.Web/wwwroot/css/site.css`

- [ ] Remove reason-tag chips from grid cards.
- [ ] Rename visible score labeling to `Taste fit`.
- [ ] Keep the card layout compact and cleaner while preserving useful context.

### Task 5: Verify and package

**Files:**
- Output: `build/LocalMovieVault.Web`
- Output: `G:\My Drive\MasterApp\Incoming`

- [ ] Run the test project and confirm all checks pass.
- [ ] Publish the web app build.
- [ ] Create a new zip for the updated version.
- [ ] Copy the zip to `G:\My Drive\MasterApp\Incoming`.
