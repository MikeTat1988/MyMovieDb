# Review Evidence Audit for Current Mismatches

Date: 2026-04-25

Purpose: audit whether real external review evidence can explain current high-score mismatches before connecting review-derived signals to scoring.

Important limitation: the local settings file currently has `MetadataProviders:TmDb:ApiKey = null`, so this audit used public indexed TMDb review pages and other public review/rating pages. The production implementation should use the TMDb API endpoint once a key is configured, then store compact derived signals rather than raw review text.

## Summary

| Movie | Current app prediction | User signal | Review evidence usefulness | Expected score impact after full connection |
| --- | ---: | --- | --- | --- |
| The Room | 92.2 / Loved | not in live DB as rated | Very high: external reviews/ratings clearly identify severe craft failure and cult-bad context. | Strong drop, likely 30-45 unless the user explicitly wants cult-bad watches. |
| Genocyber | 90.7 / Loved | 20 / Couldn't finish | Medium-high: reviews identify extreme gore, confusing story, weak characterization, bad dub risk. | External review layer should lower confidence/top-band; with user negative anchor, similar titles should drop hard. |
| Lucia | 90.0 / Loved | 20 / Couldn't finish | Low-medium: external consensus is mostly positive; a minority user-review pattern matches slow/boring/no-real-twist complaints. | Review layer alone should not punish much; needs personal negative anchor and mismatch calibration. |
| Sinners | 90.5 / Loved | 55 / Meh | Low-medium: external quality is very strong, but some reviews mention long/draggy/bloated sections. | Review layer may add pacing/runtime caution, but user Meh/mismatch must do the real correction. |
| Until Dawn | 90.3 / Loved | 55 / Meh | High: aggregate and review text mention repetition, weak inspiration, lack of scares, flawed/rushed story. | Should leave top band; likely 55-70 before personal mismatch, lower if user tags align. |

## The Room

Sources:
- TMDb page/review: `https://www.themoviedb.org/movie/17473-the-room`
- Common Sense Media: `https://www.commonsensemedia.org/movie-reviews/the-room-2003`
- Rotten Tomatoes page: `https://www.rottentomatoes.com/m/the_room`
- Deep Focus Review: `https://www.deepfocusreview.com/reviews/the-room/`

Observed external ratings:
- IMDb: 3.6 / 10 with about 98k votes
- Metascore: 9 / 100
- Rotten Tomatoes: 24%

Extracted signals:
- external quality: severe negative
- craft risks: weak acting, weak writing, weak dialogue, weak cinematography/production values, thin or arbitrary plot
- taste descriptors: melodrama, relationship drama, talky
- special cases: cult movie, unintentional comedy, so-bad-it-good
- review confidence: high

Scoring expectation:
- Current app score 92.2 is a model failure.
- Once connected, severe quality risk plus craft-risk signals should cap the film below 50 unless the user has an explicit cult-bad preference.
- Expected future score: 30-45.

## Genocyber

Sources:
- Anime Herald: `https://www.animeherald.com/2023/02/18/genocyber-animes-almost-classic-cyberpunk-horror-ruined-by-a-critical-flaw/`
- Anime World review: `https://animeworld.com/reviews/genocyber.html`
- Criticker page/reviews: `https://www.criticker.com/tv/Genocyber/`
- IMDb user reviews: `https://www.imdb.com/title/tt0158634/reviews`
- Rotten Tomatoes page: `https://www.rottentomatoes.com/m/genocyber`

Observed external ratings:
- IMDb: 6.1 / 10 with about 1.5k votes
- Rotten Tomatoes has no critic reviews and very few audience ratings.
- Criticker average percentile is around mid/low range, not a strong broad endorsement.

Extracted signals:
- external quality: limited/mixed evidence
- craft risks: confusing or convoluted story, weak characterization, thin substance, bad English dub risk
- taste descriptors: extreme gore, body horror, nihilistic, disturbing, ultraviolent, stylish old anime
- positives: some reviewers mention creativity, striking animation/design, and emotional/disturbing impact
- special cases: polarizing cult/OVA shock value
- review confidence: medium

Scoring expectation:
- Review layer should not call this universally bad, but it should prevent "dark/dread/serious" from becoming 90+ by itself.
- External review signals should add warnings/caps around confusion, gore extremity, and story weakness.
- With the user's actual 20 rating, negative-anchor logic should dominate.
- Expected external-only future score: 45-60.
- Expected score after personal negative-anchor/mismatch logic: 30-50 for similar candidates.

## Lucia

Sources:
- Rotten Tomatoes page/reviews: `https://www.rottentomatoes.com/m/lucia_2013`
- IMDb user reviews: `https://www.imdb.com/title/tt2358592/reviews/`
- TMDb page: `https://www.themoviedb.org/movie/219343-lucia`

Observed external ratings:
- IMDb: 8.3 / 10 with about 13k votes
- Rotten Tomatoes has one critic review around 7.5/10 and positive audience snippets.
- TMDb page exposes themes/keywords but no obvious review sample in indexed result.

Extracted signals:
- external quality: positive
- craft positives: original/complex concept, strong screenplay/editing, layered dream/reality structure
- taste descriptors: complex, fast-paced for some reviewers, romance/sci-fi/fantasy hybrid, dream/reality ambiguity
- minority risks: some user reviews mention slow/boring, no real twist, romance stereotypes, overrated
- review confidence: medium-low because broad review text is sparse and skewed positive

Scoring expectation:
- Review layer alone should not strongly penalize Lucia; external evidence says many viewers liked the craft.
- The correction must come from the user's own 20 rating, negative tags, and mismatch calibration.
- Review-derived signal can add "polarizing/complex/romance-heavy" caution, not a severe quality penalty.
- Expected review-only future score: still probably 70-85 if personal evidence is ignored.
- Expected score with user negative anchor/mismatch: 50-65, lower for candidates matching the disliked aspects the user tags.

## Sinners

Sources:
- Rotten Tomatoes page: `https://www.rottentomatoes.com/m/sinners_2025`
- TMDb review page: `https://www.themoviedb.org/movie/1233413-sinners/reviews`
- TMDb page: `https://www.themoviedb.org/movie/1233413-sinners`

Observed external ratings:
- IMDb: 7.5 / 10 with high vote count
- Metascore: 84 / 100
- Rotten Tomatoes: 97% critics, 96% audience

Extracted signals:
- external quality: strong positive
- craft positives: visual storytelling, music, genre fusion, ambitious original blockbuster, strong action/horror craft
- taste descriptors: long, blues/music-heavy, Southern Gothic, vampire horror, action/drama hybrid
- minority risks: some TMDb/user review snippets mention draggy middle, long runtime, bloated/extended ending, storyline not exciting enough for them
- review confidence: high for quality, medium for pacing-risk specifics

Scoring expectation:
- Review layer should not punish this as low-quality.
- It can identify pacing/runtime/tone risks that align with the user's Meh rating.
- The real correction must come from user Meh, tags, and mismatch calibration.
- Expected external-only future score: 80-90.
- Expected score with personal Meh/mismatch: 60-75, not 90+.

## Until Dawn

Sources:
- Rotten Tomatoes page/reviews: `https://www.rottentomatoes.com/m/until_dawn_2025`
- Metacritic page: `https://www.metacritic.com/movie/until-dawn/`
- TMDb review page: `https://www.themoviedb.org/movie/1232546-until-dawn/reviews`
- Guardian review: `https://www.theguardian.com/film/2025/apr/24/until-dawn-review-video-game-horror`

Observed external ratings:
- IMDb: 5.7 / 10 with about 61k votes
- Metascore: 47 / 100
- Rotten Tomatoes: 51% critics, 66% audience

Extracted signals:
- external quality: mixed/medium risk
- craft risks: repetitive structure, weak/lack-of-inspiration variations, not scary enough, poorly told/jumbled story, rushed ending, underdeveloped characters
- craft positives: decent technical craft, atmosphere, grisly deaths/effects, fast pacing in some reviews, competent direction
- taste descriptors: video-game adaptation, time-loop horror, slasher/body horror, teen ensemble
- review confidence: high

Scoring expectation:
- Review layer should materially lower this from top band.
- Genre-adjusted horror handling should avoid overpunishing 5.7 as automatic trash, but RT/Metacritic/review text should still flag medium risk.
- Expected external/review future score: 55-70.
- With user Meh and tags, expected score should lean lower if user's reasons match "not scary", "weak story", "no tension", or "flat atmosphere".

## Implementation Lessons

1. Task 1 was only scaffolding plus a The Room regression fixture. It was not a full review reader.
2. A production review layer needs TMDb API fetching or another safe review source; otherwise it is biased toward known cases and indexed snippets.
3. Review evidence helps when reviewers identify craft problems or polarizing taste descriptors.
4. Review evidence should not override the user's own ratings. `Lucia` and `Sinners` prove why: both have positive external consensus but were user mismatches.
5. The scoring pipeline should separate:
   - external quality risk
   - review-derived craft/taste descriptors
   - user negative anchors
   - mismatch calibration

