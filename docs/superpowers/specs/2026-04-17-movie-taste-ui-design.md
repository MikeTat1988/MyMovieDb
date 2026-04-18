# Movie Taste UI And Scoring Design

**Goal**
Make the library UI cleaner and make recommendation scores feel believable and personal for the current user taste profile.

**Approved direction**
- Remove reason tags from the library grid.
- Replace the current confusing `Match` presentation with a clearer `Taste fit` concept.
- Separate personal taste fit from the overall recommendation score.
- Reweight the taste system so `Twist`, `Unexpected ending`, `Original story`, and `Cinematography` have stronger influence than broad metadata such as genre overlap.
- Keep this work local and deterministic so a future Ollama layer can build on top of it rather than replace it.

**Design**

1. **Cleaner grid cards**
   Library cards will show only a small set of signals: IMDb, `Taste fit`, verdict or prediction label, and one compact explanation line. Reason tags remain editable on the details page but do not appear on the grid.

2. **Separate score meanings**
   `PersonalMatchScore` will become the internal backing value for `Taste fit`, describing how strongly a movie aligns with the user's taste profile.
   `PredictedScore` will represent watch priority or overall recommendation after adding softer signals such as public quality/confidence and exploration bonus.

3. **Taste profile tuned to user preferences**
   The quick-reason tag vocabulary will be updated to better reflect the user's actual taste. Existing generic labels can be renamed when helpful. The strongest positive tags should emphasize:
   - `Twist`
   - `Unexpected ending`
   - `Original story`
   - `Cinematography`

4. **Scoring model changes**
   The current model starts too high and makes it too easy for many titles to look nearly perfect. The revised model will:
   - lower the baseline,
   - cap the effect of broad metadata,
   - favor direct taste evidence over generic overlap,
   - keep `Taste fit` and `PredictedScore` distinct,
   - avoid handing out `100` without very strong evidence.

5. **Verification**
   Add regression tests proving:
   - personalized titles outrank generic same-genre titles,
   - `Taste fit` and `PredictedScore` can differ,
   - the new tag vocabulary includes the user's preferred concepts.
