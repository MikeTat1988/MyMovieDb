# Taste Priorities Recommendation Design

Date: 2026-04-18
Project: MyMovieDB / LocalMovieVault
Status: Approved design, updated with taste-model notes, ready for implementation planning after user review

## Goal

Improve recommendation accuracy without rewriting the recommendation engine structure.

The app must continue to show the app's predicted score for every movie, including watched and rated movies. User verdicts remain training input only; they must not replace the displayed prediction.

The main product addition is a `Taste Priorities` settings UI with 4 dropdowns for user-selected important tags. These settings should increase the influence of those tags during recommendation scoring and trigger a full recalculation when applied.

The implementation must also expose the underlying taste-tuning inputs in a way that can later be surfaced as richer UI controls and saved presets, without requiring a structural rewrite.

This is not a score-forcing system. We are not hard-coding final score bands. The algorithm should remain logically predictive. The 4 chosen tags are additional explicit preference input that changes how evidence is weighted.

## Product Intent

The user wants the model to "understand" taste over time.

Desired behavior:
- Watched and rated movies continue to display the app prediction number, not the user verdict.
- As more ratings and tags accumulate, predictions on watched movies should move toward the user's actual taste.
- Loved movies should eventually trend into the `90+` range when enough supporting evidence exists.
- Liked movies should tend toward roughly the `75-85` range.
- Meh movies should tend toward roughly the `60-70` range.
- Couldn't finish movies should fall below `50`, often substantially.

These are validation targets for algorithm quality, not direct programmed output bands.

Additional product direction from brainstorming:
- the first implementation may remain single-profile
- but the tuning inputs must be organized so they can later support multiple saved taste presets
- future UI should be able to expose and "play with" these tunable elements without replacing the engine

## Current Problem Summary

The current engine already recalculates globally after reviews, but prediction movement can feel too weak or too narrow because:
- explicit tags compete with many weaker metadata signals
- newly reviewed movies with no tags contribute less preference signal
- inferred keyword overlap can dominate in cases where the user expects more personal taste influence
- there is no strong explicit user preference channel apart from accumulated verdict/tag history

The result is that some movies the user loves can remain predicted too low even after multiple ratings.

The current engine is also too generic in some preference tradeoffs for this user profile:
- `Original idea` and `Incredible visuals` should be unusually strong cross-genre positives
- favorite genre alone should not strongly help if the movie is generic
- `Good pacing` should be close to baseline, while `Too slow` and "felt too long" should matter more
- `Tense` should matter across genres, not only horror
- `Disturbing atmosphere` should remain a meaningful horror-specific positive even when a movie is not scary

## Non-Goals

The following are explicitly out of scope for this pass:
- replacing the deterministic engine with a new architecture
- adding embeddings, external ML, or model inference
- hard-coding score bands by verdict
- replacing prediction numbers on watched cards with user verdicts
- expanding the UI into a full profile editor
- adding quick-edit important tags inside the review modal
- implementing multi-profile preset switching in this pass

## User Experience Design

### Settings Addition

Add a new settings section named `Taste Priorities`.

This should be visually polished and clear, not a plain form block. It should feel like a high-value control surface because it directly affects recommendations.

Recommended visual structure:
- section title: `Taste Priorities`
- short helper text:
  - example: `Choose up to 4 tags that matter most to you. These tags will influence recommendation predictions more strongly.`
- 4 dropdown rows:
  - `Priority 1`
  - `Priority 2`
  - `Priority 3`
  - `Priority 4`
- a preview card below the dropdowns
- an `Apply` action that saves settings and recalculates all movies

Implementation note:
- this UI is only the first exposed control surface
- the settings/domain model should be written so additional taste controls can be added later without redesigning the storage shape

### Dropdown Behavior

Each dropdown uses the canonical reason tag list already defined in the app.

Rules:
- blank is allowed for any slot
- duplicate tags are not allowed across the 4 dropdowns
- selected values must persist in settings
- changing dropdowns updates the preview before Apply

### Preview Behavior

The UI should show the impact of the selected priorities on one currently misaligned movie already tracked by the app.

Example:
- `Terminator: 56 -> 79`

Purpose:
- make the effect understandable before the user commits
- create confidence that the priorities actually influence the model

Preview requirements:
- preview one representative "off" movie at a time
- use existing mismatch-tracked movies if available
- if no mismatch candidate exists, show a fallback explanation rather than a fake number
- the preview must use the same scoring logic planned for actual recalculation, not a made-up UI estimate

Fallback message example:
- `No mismatch candidate is available for preview right now. Apply to recalculate the full library.`

### Watched Movie Cards

No change to the core display rule:
- cards continue to show the app's prediction score
- this includes watched, tagged, and rated movies

The user wants those scores visible precisely because they are a test of model quality.

## Data Model Design

### Settings Storage

Store the 4 important tags in the existing user settings JSON, not in the movie table.

Add a new settings field:
- `ImportantTags`

Recommended shape:
- JSON array of strings
- max length 4
- canonical tag labels only

Example:
```json
{
  "ImportantTags": [
    "Great twist",
    "Original idea",
    "Incredible visuals",
    "Tense"
  ]
}
```

Validation rules:
- trim values
- canonicalize through existing tag normalization logic
- remove blanks
- remove duplicates
- limit to 4

This preserves backward compatibility with current settings.

### Preset-Ready Structure

Although this pass remains single-profile, the settings object should be organized to be preset-ready.

Recommended direction:
- keep the current active settings format backward-compatible
- encapsulate taste-tuning values so they could later be moved under a named profile object

Example future-compatible shape:
```json
{
  "ImportantTags": [
    "Great twist",
    "Original idea",
    "Incredible visuals",
    "Tense"
  ],
  "TasteTuning": {
    "Version": 1
  }
}
```

The important requirement is not this exact schema, but that tuning values are grouped intentionally rather than scattered ad hoc across unrelated settings fields.

## Recommendation Engine Design

### Core Principle

Keep the existing deterministic scoring structure.

Do not add a new stage that forces scores into specific verdict bands.

Instead:
- preserve the current feature extraction and weighting pipeline
- amplify explicit user-selected important tags during scoring
- let the changed inputs naturally shift predictions over time

This pass should also make selected taste-shaping elements explicit in code so they can later be surfaced in UI and presets.

### Important Tag Influence

Current engine behavior already contains a user preference multiplier hook:
- `GetReasonTagPreferenceMultiplier(...)`
- `preferences.GetImportantTags()`

This pass should formalize and expose that capability through the UI and settings flow.

## Taste-Shape Rules From Brainstorming

The following user-taste rules were established and should guide tuning choices for this implementation and any near-term follow-up work.

### Strongest Cross-Genre Positives

For this user:
- `Original idea` and `Incredible visuals` are co-equal top-tier signals
- each can significantly lift a movie on its own
- together they usually imply at least `Liked`, unless the movie has severe failures

### Dialogue / Acting Relationship

For this user:
- `Great dialogue` is important but lower than originality and visuals
- `Great dialogue` becomes significantly stronger when paired with `Great acting`
- a movie lacking originality and visuals can still reach `Liked` if dialogue and acting are both strong

### Genre Relationship

For this user:
- favorite genres include sci-fi, horror, thriller, and action
- favorite genre alone should not strongly boost a generic movie
- strong execution can override genre in both directions
- excellent movies outside the usual genres can still score very highly

Example implication:
- a war movie like `1917` can still fit the taste model if it is exceptional in the right ways

### Pacing Relationship

For this user:
- `Good pacing` is mostly baseline competence and should not earn a large positive boost
- `Too slow` is meaningful negative evidence
- `Too long` should not be inferred from runtime alone
- `Too long` should mean that the movie feels bloated, filled with filler, or boring relative to its content quality

### Horror / Tension Relationship

For this user:
- `Scary` is a distinct horror-specific quality
- not all good horror is scary
- `Tense` is broader and should matter across genres, not only horror
- `Disturbing atmosphere` is a strong horror-positive even when the movie is not scary

These distinctions should be preserved in tuning and future UI exposure.

### Weighting Strategy

Important tags should matter more, but not overwhelm all other evidence.

Recommended multiplier range:
- default important tag multiplier: `1.75x`

Rationale:
- stronger than ordinary per-movie tag evidence
- big enough that the user feels the effect
- not so large that a single chosen tag dominates the entire score regardless of genre, similarity, or negatives

Recommended rule:
- if a candidate movie has a reason-tag hint matching an important tag, multiply that tag factor by `1.75`
- if multiple important tags match, allow cumulative effect through existing per-tag contributions
- still keep genre-aware multipliers and ordinary tag weights active

This means:
- important tags are enhanced
- ordinary selected reason tags on rated movies still matter
- the algorithm stays additive and interpretable

### Exposed Tunable Elements

Even if only `ImportantTags` is exposed in the first UI, the implementation should make these tuning elements easy to expose later:
- important-tag multiplier
- relative weight of positive explicit tags versus inferred hints
- relative weight of genre-only affinity
- relative weight of director/writer affinity
- relative weight of cross-genre anchor tags such as `Original idea` and `Incredible visuals`
- relative weight of negative pacing evidence like `Too slow` and `Too long`
- relationship multiplier for tag combinations such as `Great dialogue + Great acting`

These do not all need UI in this pass, but they should not be buried as magic numbers scattered across unrelated methods if avoidable.

### Negative Evidence

Important tags should not only boost positives. If the user chooses a tag that has a meaningful opposite or related negative evidence, the system should still preserve negative reasoning from reviewed movies.

This pass should not invent new negative-opposite mapping beyond the current catalog. We should keep the current sign behavior:
- positive tag evidence boosts
- negative tag evidence penalizes

### Prediction Preview

The settings preview should compute a temporary score using:
- current library
- current rated movies
- pending important tag selections
- one mismatch candidate movie

This preview should not persist anything until Apply is pressed.

Implementation constraint:
- do not fork a separate scoring formula for preview
- reuse the same engine logic with temporary preferences

## Mismatch Candidate Selection

The preview should use one movie already known to be "off" in prediction.

Preferred candidate selection:
1. watched movies with a strong user verdict and a large gap between predicted score and expected verdict band
2. if multiple exist, choose the one with the largest mismatch
3. if none exist, optionally fall back to a recommendation candidate with unusually strong relevant overlap

Expected verdict bands for mismatch detection:
- Loved: target center near `92`
- Liked: target center near `80`
- Meh: target center near `65`
- Couldn't finish: target center near `35`

These are preview diagnostics only, not hard-coded scoring outputs.

This mismatch list can remain internal unless the app already exposes it elsewhere.

## Apply Flow

When the user presses `Apply`:
1. validate and normalize the 4 selected important tags
2. save to settings JSON
3. run full recommendation recalculation
4. return success state
5. refresh settings UI and dependent scores

User feedback:
- while recalculating, show clear temporary state such as `Recalculating predictions...`
- when complete, show success confirmation

Because recalculation can take noticeable time, the UI should not feel frozen.

## Controller / Service Boundaries

The implementation should stay close to the existing structure.

Recommended boundaries:
- Settings UI reads/writes the new `ImportantTags` setting
- existing settings controller handles submit/apply
- recommendation engine reads important tags from preferences service
- preview computation should live in a service or helper close to settings/recommendation logic, not in the Razor view

Avoid:
- duplicating scoring logic in JavaScript
- computing fake preview values entirely on the client
- adding large new abstractions if existing services can support this cleanly

Prefer:
- collecting tuning weights into a focused, named configuration structure or helper
- keeping the engine generic, with user/profile preferences shaping outcomes
- making future preset support a straightforward extension rather than a rewrite

## Explanation / Transparency

No change is required to card display text in this pass, but the recommendation context and explanation system should continue to reflect tag-based influences.

If an important tag materially helps a prediction, it is acceptable and desirable for that to appear in explanation context through existing `tag:` factors.

## Testing Requirements

This pass needs focused regression coverage.

Minimum tests:
- settings important tags normalize, dedupe, and limit to 4
- recommendation scoring changes when an important tag matches candidate tag hints
- preview calculation uses temporary settings without persisting them
- applying settings persists important tags and triggers full recalculation
- watched movie cards still display predicted score rather than user verdict
- tuning helpers remain deterministic and preset-ready

Suggested behavior tests:
- a candidate with matching important tags scores higher than the same candidate without those preferences
- a non-matching candidate does not receive the same uplift
- duplicate dropdown selection is prevented or normalized away

## Risks

### Over-amplification

If the multiplier is too high, one chosen tag can distort too many recommendations.

Mitigation:
- start at `1.75x`
- verify with preview and smoke tests

### Preview / final score mismatch

If preview uses separate logic from recalc, trust in the UI will drop.

Mitigation:
- reuse real scoring logic for preview

### Settings clutter

If the UI looks like a low-value form, the feature will feel technical rather than useful.

Mitigation:
- present it as a premium, well-spaced settings card with a clear before/after preview

## Why This Design Fits The Constraint

This design is intentionally token-cheap relative to a major rewrite because it:
- keeps the deterministic engine
- reuses existing tag definitions
- reuses existing preferences service
- reuses existing recalculation flow
- adds one explicit, high-signal user preference channel instead of a new recommendation architecture
- prepares later preset support without requiring us to build it now

## Open Implementation Notes

The implementation should inspect whether the important-tag multiplier hook already exists but is effectively dormant due to missing UI or missing settings persistence. If so, prefer wiring and strengthening it over introducing a second competing preference mechanism.

The implementation should also preserve the current rule that watched movies display prediction numbers. That behavior is intentional and is part of how the user evaluates algorithm quality.

The implementation should avoid turning this into a user-specific hardcoded engine. Taste-specific behavior should come from saved preference inputs and tuning structures that could later be represented as presets.

## Acceptance Criteria

This work is successful when:
- Settings shows a polished `Taste Priorities` section with 4 dropdowns
- the user can choose up to 4 canonical tags
- a preview movie shows a believable before/after predicted score impact
- pressing Apply saves the selections and recalculates all movie predictions
- prediction numbers continue to show on watched movies
- chosen important tags materially influence scores without overwhelming the model
- the system remains structurally close to the current implementation
- the new tuning path is organized so richer UI controls and saved presets can be added later with low friction
