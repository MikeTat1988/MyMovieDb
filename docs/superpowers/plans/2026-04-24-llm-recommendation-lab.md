# LLM Recommendation Lab Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a detachable local browser lab that compares MovieDb raw recommendation evidence with Ollama explanations for existing unwatched movies.

**Architecture:** Create a standalone ASP.NET Core minimal app in `llm-lab/` that references `src/LocalMovieVault.Web`, reuses existing storage and recommendation services, and serves a static UI. Keep all prototype code removable without changing the main product.

**Tech Stack:** .NET 8, ASP.NET Core minimal APIs, EF Core SQLite, vanilla HTML/CSS/JS, Ollama HTTP API.

---

### Task 1: Lab Host

**Files:**
- Create: `llm-lab/LocalMovieVault.LlmLab.csproj`
- Create: `llm-lab/Program.cs`
- Create: `llm-lab/README.md`

- [ ] Create a .NET 8 web app project that references `../src/LocalMovieVault.Web/LocalMovieVault.Web.csproj`.
- [ ] Register MovieDb storage, SQLite `AppDbContext`, recommendation services, and an `HttpClient` for Ollama.
- [ ] Add endpoints for `/api/health`, `/api/models`, `/api/candidates`, and `/api/analyze`.

### Task 2: Analysis Services

**Files:**
- Create: `llm-lab/Services/LabRecommendationService.cs`
- Create: `llm-lab/Services/OllamaClient.cs`

- [ ] Select unwatched, non-dismissed, non-review-needed movies ordered by predicted score.
- [ ] Parse `RecommendationContextJson` into positives, negatives, warnings, and anchors.
- [ ] Build compact prompts for Ollama and Codex comparison.
- [ ] Measure elapsed milliseconds for raw app loading and Ollama calls.

### Task 3: Local UI

**Files:**
- Create: `llm-lab/wwwroot/index.html`
- Create: `llm-lab/wwwroot/styles.css`
- Create: `llm-lab/wwwroot/app.js`

- [ ] Show controls for candidate count, Ollama model, and temperature.
- [ ] Render side-by-side raw MovieDb output, Ollama output, Codex prompt/manual lane, and timing.
- [ ] Allow prompt/response inspection per movie.
- [ ] Handle Ollama connection errors visibly without breaking raw MovieDb output.

### Task 4: Verification

**Files:**
- Modify as needed only under `llm-lab/`.

- [ ] Run `dotnet build llm-lab/LocalMovieVault.LlmLab.csproj`.
- [ ] Run the lab locally and call `/api/health`.
- [ ] If Ollama is reachable, call `/api/models`; if no models are installed, verify the UI reports that clearly.
