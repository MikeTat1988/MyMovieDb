# LLM Recommendation Lab Design

## Goal
Build a detachable local lab for comparing raw MovieDb recommendation output with Ollama-written taste explanations for unwatched movies already in the database.

## Scope
- Keep all prototype code under `llm-lab/`.
- Do not integrate into the main app navigation yet.
- Use existing MovieDb data, eligibility rules, prediction scores, tags, and recommendation context.
- Show raw app output next to Ollama output, prompts, responses, and elapsed timing.
- Include a manual Codex comparison lane with prompt copy/paste and a stopwatch-style timer, because this MVP does not assume an OpenAI API key or Codex API wiring.

## Architecture
`llm-lab` is a small ASP.NET Core minimal web app that references `src/LocalMovieVault.Web`. It registers the same database and recommendation services, serves a static HTML/JS UI, and exposes JSON endpoints for candidates, Ollama models, and analysis runs.

## Data Flow
1. The lab loads the user's live MovieDb database through `AppStorageBootstrapper`.
2. It recalculates predictions before reading candidates.
3. It selects unwatched, non-dismissed, non-review candidates ordered by existing predicted score.
4. It builds compact evidence packets with raw score, reason, tags, factors, and liked/disliked anchors.
5. It sends one structured prompt per candidate to Ollama.
6. It returns raw MovieDb data, Ollama result, Codex prompt text, and timings to the UI.

## Testing
- Build the lab project.
- Verify the raw candidates endpoint returns JSON.
- Verify the UI can load even if Ollama has no models or the Ollama request fails.
