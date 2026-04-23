# Prediction System Redesign

## Goal

Improve movie recommendation quality so the top-ranked unwatched titles better match the user's real taste, especially when a movie should rank highly without requiring perfect keyword or tag overlap.

## Current Problems

The current deterministic engine is stable, but it has three structural issues:

1. `Meh` ratings act as negative evidence, which suppresses broad categories the user may still like occasionally.
2. Display scores are hand-mixed from several partial scores, so a movie can rank near the top but still show a surprisingly low number.
3. Strong results depend too heavily on explicit tag and keyword matches, which makes good candidates with incomplete metadata look weaker than they should.

## Non-Goals

- Replace the deterministic engine with ML or hosted inference.
- Change the database schema.
- Rebuild the explanation UI.
- Change import/export semantics.

## Recommended Approach

Keep the deterministic engine, but split the recommendation into three concepts:

1. `Affinity`: how strongly the candidate matches the user's positive and negative taste signals.
2. `Confidence`: how much evidence the engine actually has for this candidate.
3. `Display score`: a calibrated score derived from affinity rank and confidence, so the number shown to the user matches the ordering better.

This preserves the current architecture and data flow while fixing the main ranking and calibration problems.

## Evidence Model

User feedback should contribute to the taste profile with these semantics:

- `Loved`: strong positive evidence
- `Liked`: mild positive evidence
- `Meh`: neutral evidence
- `Couldn't finish`: strong negative evidence

This means `Meh` titles no longer push genres, creators, and plot features downward. They simply do not strengthen the profile.

The recommendation output shown on movie cards must remain separate from the user's real feedback, regardless of whether the movie is watched or unwatched:

- the card should always show the system's own match score and recommendation label
- the user's real score/grade/verdict remains a separate data point used as training evidence after watching
- the system match score must not be overwritten or reinterpreted as the user's real score
- keeping both values independent is a product feature because it lets the user compare prediction versus reality and judge whether the system is working

## Scoring Model

### 1. Raw Affinity

For each candidate, compute a raw affinity score from:

- genre matches
- genre-pair matches
- director/writer/actor overlap
- plot keyword overlap
- explicit and inferred reason-tag overlap
- similarity to positively rated watched titles
- penalties for similarity to strongly negative watched titles

Important taste tags remain a boost, but only as a secondary adjustment on top of affinity.

### 2. Confidence

Compute a separate confidence score from:

- metadata completeness
- external quality signals already available in metadata
- count of meaningful positive matches
- count of meaningful negative matches

Confidence should answer "how much evidence do we have?" rather than "how much will the user like it?"

### 3. Calibrated Display Score

Convert affinity into the user-facing score with a percentile-style calibration across the current movie set:

- higher-affinity candidates should reliably land above lower-affinity candidates
- top candidates should be able to reach high scores without perfect tag overlap
- low-confidence candidates should still rank correctly, but their displayed score should be moderated slightly

The displayed score should remain deterministic for the same library state and preferences.

## Ranking Rules

- `Best Match` ordering should be driven primarily by affinity.
- Displayed score should remain consistent with ordering.
- Confidence should affect display calibration and explanations, not dominate ranking.
- Dismissed and review-needed filtering remains unchanged.
- Watched cards and unwatched cards should both continue to show the system match score rather than swapping to the user's own score.

## UI Rules

The card UI should make prediction-versus-reality comparison easy without adding noise:

- every movie card should show the system match score
- IMDb score should remain visible in the same metadata row
- the user's own score should appear only after the review process is complete
- until review is complete, the UI should avoid showing a provisional user score

For reviewed watched titles, the metadata line should present these values together in a clean aligned way:

- IMDb
- Match
- User

The row should be visually balanced, easy to scan, and readable on both desktop and mobile. The UI should feel intentional and polished rather than squeezed or text-heavy.

## Explanation Rules

Keep the current explanation shape:

- top positive reasons
- top negative risk
- similar liked titles

Update the explainer copy only if needed to reflect confidence-aware language, for example "strong evidence" versus "limited evidence".

## Code Changes

### RecommendationCatalog

- Change grade weights so `Meh` contributes zero instead of negative value.
- Keep strong negative weight for `Couldn't finish`.

### DeterministicRecommendationEngine

- Separate raw affinity computation from confidence computation.
- Replace the current final hand-mixed formula with calibrated display scoring.
- Preserve existing result fields:
  - `PersonalMatchScore`
  - `PredictedScore`
  - `PredictedLabel`
  - `PredictedReason`

`PersonalMatchScore` will hold the affinity-oriented score, while `PredictedScore` will hold the calibrated user-facing score.

For watched titles, the engine must continue using the user's real feedback as the source of truth when building the taste profile. `PredictedScore` and `PredictedLabel` remain independent model outputs for both watched and unwatched titles.

### RecommendationFeatureExtractor

- Reuse the current metadata quality and quality-confidence signals as confidence inputs.
- Avoid schema changes or new persisted fields for this iteration.

### Tests

Add regression coverage for:

- `Meh` does not penalize matching candidates
- a strong candidate can score high without perfect explicit tag overlap
- calibrated display scores stay aligned with ranking
- strongly negative evidence still suppresses clearly bad matches
- watched-title user feedback remains the training source of truth, independent from displayed system match labels
- watched and unwatched cards both continue displaying the system match score rather than the user's own score
- user score appears in the card UI only after review is complete

## Risks

### Risk: Percentile calibration can feel unstable

If calibration depends on the whole library, a new import may shift many displayed scores.

Mitigation:

- use deterministic normalization rules
- bias display calibration toward stable bands rather than extreme spread
- keep affinity ordering stable even if display numbers move modestly

### Risk: Confidence can hide good obscure titles

If confidence is too strong, sparse-metadata movies may still look unfairly weak.

Mitigation:

- confidence should soften display scores, not dominate affinity
- top-affinity candidates must still surface near the top even with thinner metadata

## Verification Plan

Before finishing implementation:

- run targeted smoke tests covering new recommendation behavior
- run the full existing smoke suite
- run a project build
- if the update is shipped, run `build-release.bat`

## Success Criteria

The redesign is successful if:

1. movies that match liked taste signals but lack perfect tag overlap rank materially better than before
2. `Meh` history no longer drags down broad categories the user still enjoys
3. top-ranked movies can display convincingly high scores
4. the engine remains deterministic and understandable
