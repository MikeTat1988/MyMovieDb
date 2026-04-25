# MovieDb Workspace Guide

## Project intent
- This project is a local-first ASP.NET Core MVC movie recommendation app.
- Optimize for a clean workspace, deterministic behavior, and additive data changes.

## Workspace hygiene
- Do not create ad-hoc output folders like `package-v2`, `package-v3`, `package-v4`, `package-v5`, or similar versioned dump directories.
- Keep publish/build output inside `build/LocalMovieVault.Web` unless the user explicitly asks for a different destination.
- The canonical packaging command is `build-release.bat`. After any shipped update, run it so the app is published, zipped, and copied to `G:\My Drive\MasterApp\Incoming`.
- `build-release.bat` now stages publish and zip output before copying to the canonical build/package paths. If the local canonical zip or publish folder is locked, the script should still deliver the fresh zip to `Incoming` and log a warning instead of failing delivery.
- The shipped zip must contain `app.manifest.json` at the package root, and that packaged manifest must be installable by MasterApp as a portable app rather than the workspace `source` manifest.
- Temporary logs belong in existing log files or should be deleted before finishing work.
- Before finishing a task, remove disposable artifacts created during the task if they are not part of the product.

## File discipline
- Reuse existing folders and patterns before creating new top-level directories.
- Prefer adding new views/partials/services over mutating legacy mixed-encoding files when a clean replacement is safer.
- Keep root-level files minimal and intentional.

## Product rules
- Import must remain additive and must not destructively overwrite the library.
- Export should be user-readable and useful for LLM analysis.
- Recommendation UX should stay movie-first, poster-first, and low-noise.
- `Best Match` and `Surprise me` should exclude dismissed and review-needed titles by default.

## LLM collaboration rules
- Before implementing, inspect current controllers, models, views, and tests.
- For fast orientation, start with `docs/LLM_CONTEXT.md` before scanning the whole app.
- For recommendation weight, hybrid/tone, anchor, evidence-wording, or preview-score tuning work, use the installed Codex skill `moviedb-recommendation-tuning` if available.
- After implementing, run a build and relevant tests before claiming success.
- Before every shell command, estimate runtime, tell the user `running ... supposed to run for ...`, and set an explicit timeout on the command. Never run open-ended commands in this workspace.
- Prefer `rg` / `rg.exe` for text and file search when available. On this machine, the Codex-bundled `WindowsApps` copy was not executable (`Access is denied`), so a standalone WinGet ripgrep install was added and should be preferred via user `PATH`.
- Never run the smoke executable directly from the shell without a repo-managed timeout. Use `scripts/run-smoke-tests.ps1`, which self-enforces build/run timeouts and kills stale `LocalMovieVault.Web.Tests` processes before and after the run.
- Do not rely on outer tool timeouts alone for smoke tests. The child test process can outlive the caller and keep locks behind.
- If the update should be delivered, run `build-release.bat` before finishing.
- If `build-release.bat` fails, inspect `logs/build-release.log` before retrying. Repeated `Access is denied` on the old local zip or publish folder usually means a stale lock on the canonical output, not a bad new build.
- If cleanup is needed, prefer the repo cleanup script over manual one-off deletions.
- If smoke/build/package flows leave lingering processes, use a dedicated cleanup script in `scripts/` first instead of ad-hoc kill commands.
- Update this file if new long-term workspace rules are introduced.
