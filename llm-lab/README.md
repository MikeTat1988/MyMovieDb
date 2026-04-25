# MovieDb LLM Lab

Detachable local experiment for comparing MovieDb raw recommendation evidence with Ollama and Codex CLI generated taste explanations.

## Run

```powershell
dotnet run --project C:\Dev\MovieDb\llm-lab\LocalMovieVault.LlmLab.csproj
```

Open `http://127.0.0.1:5099`.

## Notes

- The lab reads the same user database as the main app.
- It does not add movies, write ratings, or change app navigation.
- Ollama defaults to `http://127.0.0.1:11434`.
- The Codex lane runs the local Codex CLI directly for lab testing. It does not route through MasterApp.
- If needed, set `CODEX_CLI_PATH` to the working `codex.exe`; otherwise the lab prefers the per-user Codex package binary and falls back to `codex` on PATH.
- Codex runs use `codex exec` with a read-only sandbox, no approval prompts, an ephemeral session, and the MovieDb workspace as the working root.
- The Codex dropdown includes `gpt-5.4-mini` as the cheap baseline and `gpt-5.5` as a quality comparison model.
- Codex runs collect elapsed time plus reported input, output, and total tokens when the CLI emits usage events.
