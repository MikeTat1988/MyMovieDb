# MovieDb Workspace Guide

## Project intent
- This project is a local-first ASP.NET Core MVC movie recommendation app.
- Optimize for a clean workspace, deterministic behavior, and additive data changes.

## Workspace hygiene
- Do not create ad-hoc output folders like `package-v2`, `package-v3`, `package-v4`, `package-v5`, or similar versioned dump directories.
- Keep publish/build output inside `build/LocalMovieVault.Web` unless the user explicitly asks for a different destination.
- The canonical packaging command is `build-release.bat`. After any shipped update, run it so the app is published, zipped, and copied to `G:\My Drive\MasterApp\Incoming`.
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
- After implementing, run a build and relevant tests before claiming success.
- If the update should be delivered, run `build-release.bat` before finishing.
- If cleanup is needed, prefer the repo cleanup script over manual one-off deletions.
- Update this file if new long-term workspace rules are introduced.
