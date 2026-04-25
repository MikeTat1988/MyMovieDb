# Review-Derived Recommendation Calibration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add external review-derived and genre-adjusted quality signals to the recommendation engine so weak-evidence matches and mismatch-prone films stop inflating into the 90+ band.

**Architecture:** Start with a narrow, testable review-signal layer that converts safe metadata/review sources into structured signals. Feed those signals into recommendation features and scoring as external evidence, weaker than the user's own ratings/tags but stronger than inferred plot keywords. After validating score movement on mismatch films, proceed to dismissal semantics, mismatch calibration, and UI cleanup.

**Tech Stack:** ASP.NET Core MVC, EF Core, existing OMDb metadata flow, optional TMDb review endpoint, deterministic recommendation services in `src/LocalMovieVault.Web/Services/Recommendations`, smoke-test harness in `tests/LocalMovieVault.Web.Tests`.

---

## Current Problem Summary

The current scorer can treat surface overlap such as `Drama`, `serious`, `reflective`, `English`, and weak similarity anchors as enough evidence for a 90+ score. This produced an absurd result for `The Room`, and the live database shows the same failure mode on already-rated mismatch titles such as `Genocyber`, `Lucia`, `Sinners`, and `Until Dawn`.

The fix should not make the model train on its own guesses. Human watched ratings and explicit human reasons are strong evidence. External ratings/reviews are quality and craft evidence. System-generated dismiss suggestions are not taste evidence unless the user selects a concrete reason.

---

## Target Semantics

- `Watched + rated` is real human taste evidence.
- `Watched + low rating/tags` is a strong negative anchor.
- `Dismissed` means watched and disliked; it stays in the database, is hidden from suggestions, and remains a negative anchor.
- `Deleted` or removed means no taste signal.
- `Not watched + low estimated score` becomes `Suggested dismiss` in Review, not an automatic dismissal and not a negative anchor.
- Dismissing a not-watched suggested-dismiss film only trains the model if the user selects a specific reason; otherwise it only hides the film.
- `Mismatch` means the model was overconfident. It should calibrate confidence and overtrusted reasoning, not blindly turn all movie features into negative taste tags.

---

## File Map

- Modify: `C:\Dev\MovieDb\src\LocalMovieVault.Web\Services\Recommendations\RecommendationModels.cs`
  - Add compact models for external quality and review-derived craft signals.
- Modify: `C:\Dev\MovieDb\src\LocalMovieVault.Web\Services\Recommendations\RecommendationFeatureExtractor.cs`
  - Include review/quality signals in recommendation features.
- Modify: `C:\Dev\MovieDb\src\LocalMovieVault.Web\Services\Recommendations\DeterministicRecommendationEngine.cs`
  - Apply genre-adjusted quality penalties, craft penalties, and top-band caps.
- Modify: `C:\Dev\MovieDb\src\LocalMovieVault.Web\Services\Recommendations\RecommendationExplainer.cs`
  - Explain quality-risk/cult-warning cases without noisy text.
- Modify: `C:\Dev\MovieDb\src\LocalMovieVault.Web\Services\Recommendations\RecommendationCatalog.cs`
  - Add genre baselines and signal labels.
- Modify: `C:\Dev\MovieDb\src\LocalMovieVault.Web\Models\Movie.cs`
  - Persist compact review-signal JSON only if needed after the first in-memory test pass.
- Modify: `C:\Dev\MovieDb\src\LocalMovieVault.Web\Services\Recommendations\DatabaseSchemaMigrator.cs`
  - Add persistence columns only if `Movie.cs` gains stored review-signal fields.
- Modify: `C:\Dev\MovieDb\tests\LocalMovieVault.Web.Tests\Program.cs`
  - Add regression tests and preview diagnostics.

---

### Task 1: Add Review-Derived Signal Models and Fixture Extraction

**Files:**
- Modify: `C:\Dev\MovieDb\src\LocalMovieVault.Web\Services\Recommendations\RecommendationModels.cs`
- Modify: `C:\Dev\MovieDb\src\LocalMovieVault.Web\Services\Recommendations\RecommendationCatalog.cs`
- Modify: `C:\Dev\MovieDb\tests\LocalMovieVault.Web.Tests\Program.cs`

- [x] **Step 1: Add a failing smoke test for The Room external signals**

Create a fixture in `Program.cs` that builds a `Movie` with The Room-like OMDb data:

```csharp
var room = new Movie
{
    Id = 91001,
    Title = "The Room Fixture",
    Year = 2003,
    GenresCsv = "Drama",
    Director = "Tommy Wiseau",
    Writer = "Tommy Wiseau",
    Actors = "Tommy Wiseau, Juliette Danielle, Greg Sestero",
    Overview = "An amiable banker's perfect life is turned upside down when his bride-to-be has an affair with his best friend.",
    Country = "United States",
    Language = "English",
    RuntimeMinutes = 99,
    ImdbRating = 3.6m,
    ImdbVotes = 98635,
    Metascore = 9,
    ExternalRatingsJson = """
    [{"Source":"Internet Movie Database","Value":"3.6/10"},{"Source":"Rotten Tomatoes","Value":"24%"},{"Source":"Metacritic","Value":"9/100"}]
    """
};
```

Expected test behavior before implementation: FAIL because no extracted review/quality signal exists.

- [x] **Step 2: Add compact signal types**

Add models similar to:

```csharp
public sealed record ExternalQualitySignal(
    string Key,
    decimal Strength,
    string Source,
    bool IsRisk);

public sealed record ReviewDerivedSignals(
    IReadOnlyList<string> CraftRisks,
    IReadOnlyList<string> TasteDescriptors,
    IReadOnlyList<string> SpecialCases,
    decimal QualityRiskScore,
    decimal Confidence);
```

- [x] **Step 3: Add catalog labels and parser helpers**

Add labels for:

```text
weak-acting
weak-writing
weak-dialogue
weak-cinematography
thin-plot
unintentional-comedy
so-bad-it-good
polarizing-cult
severe-external-quality-risk
```

- [x] **Step 4: Run the targeted smoke test**

Run: `dotnet run --project C:\Dev\MovieDb\tests\LocalMovieVault.Web.Tests\LocalMovieVault.Web.Tests.csproj`

Expected: PASS for signal extraction once The Room fixture yields `severe-external-quality-risk`, `weak-acting`, `weak-writing`, and `so-bad-it-good`.

---

### Task 1.5: Audit Real Review Evidence for Mismatch Movies

**Files:**
- Create: `C:\Dev\MovieDb\docs\superpowers\specs\2026-04-25-review-evidence-audit.md`
- Modify: `C:\Dev\MovieDb\docs\superpowers\plans\2026-04-25-review-derived-recommendation-calibration.md`

- [x] **Step 1: Check local TMDb API availability**

Current user settings have `MetadataProviders:TmDb:ApiKey = null`, so production TMDb API review fetching is not available locally yet.

- [x] **Step 2: Gather real review evidence from safe public sources**

Use TMDb review pages when searchable, plus Rotten Tomatoes, Metacritic, IMDb user-review pages, and reputable review pages. Record sources and keep extracted data structured; do not treat search snippets as personal taste.

- [x] **Step 3: Extract structured signals per mismatch movie**

Audit:

```text
The Room
Genocyber
Lucia
Sinners
Until Dawn
```

For each movie, classify:

```text
external quality
craft risks
taste descriptors
polarization/cult signals
review evidence confidence
expected scoring impact once connected
```

- [x] **Step 4: Decide what review evidence can and cannot fix**

Expected outcome:

```text
Review/external quality fixes The Room-like quality failures.
Review evidence helps Genocyber and Until Dawn because reviewers mention story/pacing/fear/structure issues.
Review evidence does not fix Lucia or Sinners alone because external consensus is positive; those need personal negative anchors and mismatch calibration.
```

---

### Task 2: Add Genre-Adjusted External Quality Scoring

**Files:**
- Modify: `C:\Dev\MovieDb\src\LocalMovieVault.Web\Services\Recommendations\RecommendationCatalog.cs`
- Modify: `C:\Dev\MovieDb\src\LocalMovieVault.Web\Services\Recommendations\RecommendationFeatureExtractor.cs`
- Modify: `C:\Dev\MovieDb\tests\LocalMovieVault.Web.Tests\Program.cs`

- [x] **Step 1: Write failing tests for genre baselines**

Add tests proving:

```text
Horror IMDb 6.5 is not a severe quality risk.
Drama IMDb 6.5 is a mild/medium quality concern.
Any genre IMDb 3.6 with many votes and Metascore 9 is severe quality risk.
```

- [x] **Step 2: Add genre baseline rules**

Use conservative baselines:

```text
Horror mild risk below: 6.0
Comedy mild risk below: 6.5
Action mild risk below: 6.6
Sci-Fi mild risk below: 6.5
Thriller mild risk below: 6.7
Drama mild risk below: 7.0
Animation mild risk below: 7.0
Default mild risk below: 6.2
```

For multi-genre films, use the most forgiving baseline among present genres unless later tests prove that too loose.

- [x] **Step 3: Compute quality risk**

Implement:

```text
ratingGap = genreBaseline - imdbRating
voteConfidence increases with imdbVotes
metascorePenalty applies below 40, severe below 20
rottenTomatoesPenalty applies below 45%, severe below 30%
```

Expected The Room fixture: severe risk.

- [x] **Step 4: Run targeted tests**

Run: `dotnet run --project C:\Dev\MovieDb\tests\LocalMovieVault.Web.Tests\LocalMovieVault.Web.Tests.csproj`

Expected: PASS for genre-adjusted quality tests.

---

### Task 2.5: Make Genre Quality Calibration Easy to Tune

**Files:**
- Modify: `C:\Dev\MovieDb\src\LocalMovieVault.Web\Services\Recommendations\RecommendationCatalog.cs`
- Modify: `C:\Dev\MovieDb\tests\LocalMovieVault.Web.Tests\Program.cs`
- Modify: `C:\Dev\MovieDb\docs\LLM_CONTEXT.md`

- [x] **Step 1: Move genre quality numbers into one reachable table**

Store genre quality calibration in `RecommendationCatalog.GenreQualityCalibrations`, with one entry per genre and a default fallback.

- [x] **Step 2: Split documentation mean from penalty threshold**

Use `ExpectedMean` for future diagnostics and `MildRiskBelow` as the actual trigger threshold.

- [x] **Step 3: Apply the user-approved risk thresholds**

Current `MildRiskBelow` values:

```text
Action: 6.6
Adventure: 6.6
Animation: 7.0
Biography: 6.8
Comedy: 6.5
Crime: 6.7
Documentary: 6.8
Drama: 7.0
Family: 6.4
Fantasy: 6.5
Film-Noir: 6.7
History: 6.7
Horror: 6.0
Music: 6.6
Musical: 6.5
Mystery: 6.6
Romance: 6.5
Sci-Fi: 6.5
Short: 6.3
Sport: 6.5
Thriller: 6.7
TV Movie: 5.9
War: 6.7
Western: 6.5
Default: 6.2
```

- [x] **Step 4: Add boundary tests**

Smoke coverage verifies:

```text
Horror 6.0 is not risk, Horror 5.9 is risk.
Sci-Fi 6.5 is not risk, Sci-Fi 6.4 is risk.
Animation 7.0 is not risk, Animation 6.9 is risk.
Drama 6.5 remains risk.
IMDb 4.6 uses absolute low-imdb-rating, not duplicate genre-adjusted IMDb risk.
Slash-separated genres such as Crime/Thriller use genre calibration.
Severe external quality cases remain severe.
```

- [x] **Step 5: Document fine-tuning instructions**

Record the calibration table location and tuning workflow in `docs/LLM_CONTEXT.md`.

---

### Task 3: Apply Review/Quality Signals to Scoring and Top-Band Caps

**Files:**
- Modify: `C:\Dev\MovieDb\src\LocalMovieVault.Web\Services\Recommendations\DeterministicRecommendationEngine.cs`
- Modify: `C:\Dev\MovieDb\src\LocalMovieVault.Web\Services\Recommendations\RecommendationModels.cs`
- Modify: `C:\Dev\MovieDb\src\LocalMovieVault.Web\Services\Recommendations\RecommendationExplainer.cs`
- Modify: `C:\Dev\MovieDb\tests\LocalMovieVault.Web.Tests\Program.cs`

- [ ] **Step 1: Write failing tests for score movement**

Add tests proving:

```text
The Room fixture scores below 50 unless the user has explicit camp/so-bad-it-good preference.
A severe external quality risk cannot score 90+ from genre/tone-only evidence.
A horror movie at IMDb 6.5 with no craft risks is not unfairly capped.
```

- [ ] **Step 2: Add scoring penalties**

Apply penalties as external evidence:

```text
severe quality risk: -18 to -24
very low Metascore: -10 to -16
very low Rotten Tomatoes: -5 to -10
review-derived weak acting/writing/dialogue: -4 to -8 each, capped
so-bad-it-good: no automatic positive boost; it changes explanation and requires explicit user preference to help
```

- [ ] **Step 3: Add top-band caps**

If a candidate has severe external quality risk and lacks strong personal anchors:

```text
cap at 60 for medium evidence
cap at 50 for weak evidence
cap at 45 when craft-risk signals are severe and no explicit cult/camp preference exists
```

- [ ] **Step 4: Update explanations**

Expected explanation style:

```text
Very risky: external reviews and ratings flag weak acting, writing, and craft. This may work only as a cult so-bad-it-good watch, not as a serious drama match.
```

- [ ] **Step 5: Run targeted tests**

Run: `dotnet run --project C:\Dev\MovieDb\tests\LocalMovieVault.Web.Tests\LocalMovieVault.Web.Tests.csproj`

Expected: The Room fixture is below 50; existing strong candidates with real personal evidence still score reasonably.

---

### Task 4: Add Preview Diagnostics for Mismatch Movies

**Files:**
- Modify: `C:\Dev\MovieDb\tests\LocalMovieVault.Web.Tests\Program.cs`

- [ ] **Step 1: Extend preview output**

Add diagnostic fields to `--preview-score` or a new local diagnostic mode:

```text
EXTERNAL_QUALITY_RISK:
REVIEW_SIGNALS:
QUALITY_PENALTY:
TOP_BAND_CAP:
PERSONAL_EVIDENCE_LEVEL:
```

- [ ] **Step 2: Run diagnostics on mismatch set**

Run:

```powershell
dotnet run --project C:\Dev\MovieDb\tests\LocalMovieVault.Web.Tests\LocalMovieVault.Web.Tests.csproj -- --preview-score "The Room" "Lucia" "Genocyber" "Sinners" "Until Dawn"
```

Expected:

```text
The Room drops from 90+ behavior to below 50.
Lucia/Genocyber/Sinners/Until Dawn diagnostics show why current model overpredicted or where external signals are missing.
```

- [ ] **Step 3: Save observed before/after results**

Write a short local note only if useful under:

```text
C:\Users\micha\AppData\Local\Temp\MovieDb-Codex\
```

Do not commit temp diagnostics unless the user asks.

---

### Task 5: Implement Dismissed and Suggested-Dismiss Semantics

**Files:**
- Modify: `C:\Dev\MovieDb\src\LocalMovieVault.Web\Helpers\MovieStateHelper.cs`
- Modify: `C:\Dev\MovieDb\src\LocalMovieVault.Web\Controllers\MoviesController.cs`
- Modify: `C:\Dev\MovieDb\src\LocalMovieVault.Web\Services\Recommendations\DeterministicRecommendationEngine.cs`
- Modify: `C:\Dev\MovieDb\tests\LocalMovieVault.Web.Tests\Program.cs`

- [ ] **Step 1: Write failing state tests**

Add tests proving:

```text
Watched low-rated movies are negative anchors.
Dismissed watched movies remain negative anchors.
Not-watched low-score movies enter Review as Suggested dismiss.
Not-watched Suggested dismiss does not train the model until the user selects a reason.
```

- [ ] **Step 2: Keep suggested dismiss separate from dismissed**

`Suggested dismiss` remains computed from score threshold for not-watched movies. It should not set `IsDismissed` automatically.

- [ ] **Step 3: Treat dismissed watched movies as negative anchors**

When building the taste profile, ensure dismissed watched/rated movies are included as negative evidence according to rating/tags.

- [ ] **Step 4: Add explicit reason handling for not-watched dismissals**

If a not-watched suggested-dismiss film is hidden without a reason, do not feed it into taste profile. If a reason is selected, apply only that selected reason as weak preference evidence.

---

### Task 6: Add Mismatch Calibration Without Feedback Loops

**Files:**
- Modify: `C:\Dev\MovieDb\src\LocalMovieVault.Web\Services\AppUserPreferencesService.cs`
- Modify: `C:\Dev\MovieDb\src\LocalMovieVault.Web\Services\Recommendations\DeterministicRecommendationEngine.cs`
- Modify: `C:\Dev\MovieDb\tests\LocalMovieVault.Web.Tests\Program.cs`

- [ ] **Step 1: Write failing mismatch tests**

Add tests proving:

```text
Predicted 90 then user 20 creates calibration pressure.
Mismatch alone does not make every movie feature a negative tag.
Mismatch caps weak-evidence top-band matches.
Human tags still outweigh mismatch calibration.
```

- [ ] **Step 2: Use capped calibration penalties**

Mismatch factors should apply small penalties to overtrusted reasoning patterns:

```text
genre/tone-only match
inferred positive tag
weak anchor similarity
ignored quality risk
```

Use caps and decay/cooldown so one bad prediction does not poison a broad genre.

- [ ] **Step 3: Run smoke tests**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File C:\Dev\MovieDb\scripts\run-smoke-tests.ps1`

Expected: all smoke tests pass through the repo-managed timeout wrapper.

---

### Task 7: Final Verification and Packaging

**Files:**
- Verify: `C:\Dev\MovieDb\tests\LocalMovieVault.Web.Tests\Program.cs`
- Verify: `C:\Dev\MovieDb\logs\build-release.log`

- [ ] **Step 1: Build test project**

Run:

```powershell
dotnet build C:\Dev\MovieDb\tests\LocalMovieVault.Web.Tests\LocalMovieVault.Web.Tests.csproj
```

Expected: build succeeds.

- [ ] **Step 2: Run smoke tests**

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File C:\Dev\MovieDb\scripts\run-smoke-tests.ps1
```

Expected: smoke tests pass and no stale test process remains.

- [ ] **Step 3: Run baseline preview diagnostics**

Run:

```powershell
dotnet run --project C:\Dev\MovieDb\tests\LocalMovieVault.Web.Tests\LocalMovieVault.Web.Tests.csproj -- --preview-score "Vivarium" "District 9" "Tumbbad" "I Am the Pretty Thing That Lives in the House" "The Room"
```

Expected: established baseline films remain explainable; The Room stays out of top-band.

- [ ] **Step 4: Ship if requested**

Run:

```powershell
C:\Dev\MovieDb\build-release.bat
```

Expected: package is published, zipped, and copied to `G:\My Drive\MasterApp\Incoming`; if it fails, inspect `C:\Dev\MovieDb\logs\build-release.log` before retrying.

---

## Expected First Milestone

The first implementation milestone is complete when:

- The Room fixture has extracted external quality/review signals.
- The Room fixture scores below 50 without explicit cult/camp preference.
- Genre-adjusted IMDb does not unfairly punish normal horror ratings.
- Preview diagnostics show which quality/review signals changed the final score.
- Existing recommendation baselines do not collapse.
