# Taste Priorities Recommendation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add spec-compliant taste priorities settings and preview behavior so important tags persist cleanly, affect recommendation scoring materially, and trigger full recalculation without changing the broader UI shape.

**Architecture:** Keep the existing MVC/settings/recommendation structure, but consolidate important-tag storage and tuning into the preferences domain so preview and final recalculation share the same scoring path. Add a focused preview helper/service so the controller and Razor views stay thin and we avoid duplicate scoring formulas or stale settings fields.

**Tech Stack:** ASP.NET Core MVC, EF Core, SQLite, Razor views, existing smoke-test console harness

---

### Task 1: Lock the preferences contract and normalization

**Files:**
- Modify: `src/LocalMovieVault.Web/Services/AppUserPreferencesService.cs`
- Modify: `src/LocalMovieVault.Web/ViewModels/AppSettingsViewModel.cs`
- Test: `tests/LocalMovieVault.Web.Tests/Program.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
AssertImportantTagsNormalizeDeduplicateAndLimit();
AssertImportantTagsPersistAsCanonicalArray();
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet run --project tests/LocalMovieVault.Web.Tests/LocalMovieVault.Web.Tests.csproj`
Expected: FAIL because the current preferences model still uses `ImportantTag1..4` and does not persist a normalized `ImportantTags` array.

- [ ] **Step 3: Write minimal implementation**

```csharp
public sealed class AppUserPreferences
{
    public List<string> ImportantTags { get; set; } = [];
    public TasteTuningSettings TasteTuning { get; set; } = TasteTuningSettings.CreateDefault();

    public void Normalize()
    {
        ImportantTags = NormalizeImportantTags(ImportantTags);
        TasteTuning ??= TasteTuningSettings.CreateDefault();
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet run --project tests/LocalMovieVault.Web.Tests/LocalMovieVault.Web.Tests.csproj`
Expected: PASS for the new normalization/persistence coverage.

- [ ] **Step 5: Commit**

```bash
git add tests/LocalMovieVault.Web.Tests/Program.cs src/LocalMovieVault.Web/Services/AppUserPreferencesService.cs src/LocalMovieVault.Web/ViewModels/AppSettingsViewModel.cs
git commit -m "test: normalize taste priorities settings"
```

### Task 2: Add shared preview candidate and preview scoring logic

**Files:**
- Create: `src/LocalMovieVault.Web/Services/RecommendationPreviewService.cs`
- Modify: `src/LocalMovieVault.Web/Controllers/SettingsController.cs`
- Modify: `src/LocalMovieVault.Web/Services/Recommendations/DeterministicRecommendationEngine.cs`
- Test: `tests/LocalMovieVault.Web.Tests/Program.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
await AssertPreviewUsesTemporaryImportantTagsWithoutPersistingAsync();
AssertPreviewCandidatePrefersLargestVerdictMismatch();
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet run --project tests/LocalMovieVault.Web.Tests/LocalMovieVault.Web.Tests.csproj`
Expected: FAIL because preview currently just echoes stored scores and mismatch selection uses raw user rating gaps rather than the spec's verdict-band diagnostic.

- [ ] **Step 3: Write minimal implementation**

```csharp
public sealed class RecommendationPreviewService
{
    public RecommendationPreviewResult BuildPreview(IReadOnlyList<Movie> movies, AppUserPreferences preferences, int? previewMovieId, bool randomize)
    {
        // Select mismatch candidate, clone preferences, compute before/after with the same engine scoring path.
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet run --project tests/LocalMovieVault.Web.Tests/LocalMovieVault.Web.Tests.csproj`
Expected: PASS for temporary preview scoring and mismatch candidate coverage.

- [ ] **Step 5: Commit**

```bash
git add tests/LocalMovieVault.Web.Tests/Program.cs src/LocalMovieVault.Web/Services/RecommendationPreviewService.cs src/LocalMovieVault.Web/Controllers/SettingsController.cs src/LocalMovieVault.Web/Services/Recommendations/DeterministicRecommendationEngine.cs
git commit -m "feat: share taste priority preview scoring"
```

### Task 3: Make the engine tuning explicit and remove stale important-tag logic

**Files:**
- Modify: `src/LocalMovieVault.Web/Services/Recommendations/DeterministicRecommendationEngine.cs`
- Modify: `src/LocalMovieVault.Web/Services/AppUserPreferencesService.cs`
- Test: `tests/LocalMovieVault.Web.Tests/Program.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
await AssertImportantTagPreferenceBoostsMatchingCandidateAsync();
await AssertNonMatchingCandidateDoesNotReceiveImportantTagBoostAsync();
AssertTasteTuningDefaultsAreDeterministic();
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet run --project tests/LocalMovieVault.Web.Tests/LocalMovieVault.Web.Tests.csproj`
Expected: FAIL because the current hidden 1.35x boost is too weak and the tuning values are not grouped in a preset-ready structure.

- [ ] **Step 3: Write minimal implementation**

```csharp
private static decimal GetReasonTagPreferenceMultiplier(string label, AppUserPreferences preferences)
{
    var tuning = preferences.TasteTuning;
    return preferences.GetImportantTags().Contains(label, StringComparer.OrdinalIgnoreCase)
        ? tuning.ImportantTagMultiplier
        : 1.0m;
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet run --project tests/LocalMovieVault.Web.Tests/LocalMovieVault.Web.Tests.csproj`
Expected: PASS for matching/non-matching uplift and deterministic defaults.

- [ ] **Step 5: Commit**

```bash
git add tests/LocalMovieVault.Web.Tests/Program.cs src/LocalMovieVault.Web/Services/Recommendations/DeterministicRecommendationEngine.cs src/LocalMovieVault.Web/Services/AppUserPreferencesService.cs
git commit -m "feat: expose taste tuning for important tags"
```

### Task 4: Update the settings page and preview partial without changing the overall UI

**Files:**
- Modify: `src/LocalMovieVault.Web/Views/Settings/Index.cshtml`
- Modify: `src/LocalMovieVault.Web/Views/Settings/_SettingsPreviewCard.cshtml`
- Modify: `src/LocalMovieVault.Web/wwwroot/js/app-main.js`
- Modify: `src/LocalMovieVault.Web/wwwroot/css/site.css`
- Test: `tests/LocalMovieVault.Web.Tests/Program.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
AssertSettingsViewUsesTastePrioritiesSection();
AssertSettingsPreviewUsesRealBeforeAfterCopy();
AssertWatchedCardsStillShowPredictedScores();
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet run --project tests/LocalMovieVault.Web.Tests/LocalMovieVault.Web.Tests.csproj`
Expected: FAIL because the current view still uses legacy important-tag fields and a fake client-side range-based preview.

- [ ] **Step 3: Write minimal implementation**

```javascript
async function refreshTastePriorityPreview(form) {
  const response = await fetch('/Settings/Preview?' + new URLSearchParams(new FormData(form)));
  byId('settingsPreviewHost').innerHTML = await response.text();
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet run --project tests/LocalMovieVault.Web.Tests/LocalMovieVault.Web.Tests.csproj`
Expected: PASS for settings markup and preview behavior coverage.

- [ ] **Step 5: Commit**

```bash
git add tests/LocalMovieVault.Web.Tests/Program.cs src/LocalMovieVault.Web/Views/Settings/Index.cshtml src/LocalMovieVault.Web/Views/Settings/_SettingsPreviewCard.cshtml src/LocalMovieVault.Web/wwwroot/js/app-main.js src/LocalMovieVault.Web/wwwroot/css/site.css
git commit -m "feat: update taste priorities settings preview"
```

### Task 5: Verify, build, and package the deliverable

**Files:**
- Verify: `tests/LocalMovieVault.Web.Tests/Program.cs`
- Verify: `src/LocalMovieVault.Web/LocalMovieVault.Web.csproj`
- Output: `build/LocalMovieVault.Web`
- Output: `build/packages`

- [ ] **Step 1: Run the smoke tests**

Run: `dotnet run --project tests/LocalMovieVault.Web.Tests/LocalMovieVault.Web.Tests.csproj`
Expected: `All LocalMovieVault.Web smoke tests passed.`

- [ ] **Step 2: Run the app build**

Run: `dotnet build src/LocalMovieVault.Web/LocalMovieVault.Web.csproj`
Expected: `Build succeeded.`

- [ ] **Step 3: Run packaging**

Run: `build-release.bat`
Expected: success output showing:
- `Build: C:\Dev\MovieDb\build\LocalMovieVault.Web`
- `Zip:   C:\Dev\MovieDb\build\packages\MyMovieDB-<version>.zip`
- `Copied to: G:\My Drive\MasterApp\Incoming\MyMovieDB-<version>.zip`

- [ ] **Step 4: Inspect artifacts**

Run: `Get-ChildItem C:\Dev\MovieDb\build\packages`
Expected: packaged zip remains in the workspace in addition to the copied Incoming artifact.

- [ ] **Step 5: Commit**

```bash
git add .
git commit -m "feat: add taste priorities recommendation tuning"
```
