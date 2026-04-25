using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using LocalMovieVault.Web.Data;
using LocalMovieVault.Web.Helpers;
using LocalMovieVault.Web.Models;
using LocalMovieVault.Web.Services;
using LocalMovieVault.Web.Services.Recommendations;
using Microsoft.EntityFrameworkCore;

namespace LocalMovieVault.LlmLab.Services;

public sealed class LabRecommendationService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly AppDbContext _dbContext;
    private readonly AppUserPreferencesService _preferencesService;
    private readonly IRecommendationEngine _recommendationEngine;
    private readonly OllamaClient _ollamaClient;
    private readonly CodexCliClient _codexClient;

    public LabRecommendationService(
        AppDbContext dbContext,
        AppUserPreferencesService preferencesService,
        IRecommendationEngine recommendationEngine,
        OllamaClient ollamaClient,
        CodexCliClient codexClient)
    {
        _dbContext = dbContext;
        _preferencesService = preferencesService;
        _recommendationEngine = recommendationEngine;
        _ollamaClient = ollamaClient;
        _codexClient = codexClient;
    }

    public async Task<CandidateListResponse> GetCandidatesAsync(int count, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        await _recommendationEngine.RecalculateAsync(cancellationToken);
        var candidates = await LoadCandidatesAsync(count, cancellationToken);
        stopwatch.Stop();

        return new CandidateListResponse(candidates, stopwatch.ElapsedMilliseconds);
    }

    public async Task<AnalyzeResponse> AnalyzeAsync(AnalyzeRequest request, CancellationToken cancellationToken)
    {
        var rawStopwatch = Stopwatch.StartNew();
        await _recommendationEngine.RecalculateAsync(cancellationToken);
        var candidates = await LoadCandidatesAsync(Math.Clamp(request.Count, 1, 20), cancellationToken);
        rawStopwatch.Stop();

        var model = request.Model?.Trim();
        var results = new List<MovieLabResult>();
        var ollamaTotal = 0L;

        foreach (var candidate in candidates)
        {
            OllamaLabOutput? ollama = null;
            if (!string.IsNullOrWhiteSpace(model))
            {
                var completion = await _ollamaClient.GenerateAsync(model, candidate.OllamaPrompt, request.Temperature, cancellationToken);
                ollamaTotal += completion.ElapsedMs;
                ollama = new OllamaLabOutput(completion.Ok, completion.Text, completion.RawResponse, completion.Error, completion.ElapsedMs);
            }

            results.Add(new MovieLabResult(candidate, ollama));
        }

        return new AnalyzeResponse(
            results,
            new TimingSummary(rawStopwatch.ElapsedMilliseconds, ollamaTotal, null, rawStopwatch.ElapsedMilliseconds + ollamaTotal),
            string.IsNullOrWhiteSpace(model) ? "Choose an Ollama model to run local analysis." : null);
    }

    public async Task<AnalyzeResponse> AnalyzeWithCodexAsync(AnalyzeRequest request, CancellationToken cancellationToken)
    {
        var rawStopwatch = Stopwatch.StartNew();
        await _recommendationEngine.RecalculateAsync(cancellationToken);
        var candidates = await LoadCandidatesAsync(Math.Clamp(request.Count, 1, 10), cancellationToken);
        rawStopwatch.Stop();

        var model = string.IsNullOrWhiteSpace(request.Model) ? "gpt-5.4-mini" : request.Model.Trim();
        var results = new List<MovieLabResult>();
        var codexTotal = 0L;

        foreach (var candidate in candidates)
        {
            var completion = await _codexClient.GenerateAsync(model, BuildCodexDirectPrompt(candidate.Raw), cancellationToken);
            codexTotal += completion.ElapsedMs;
            results.Add(new MovieLabResult(candidate, null, new CodexLabOutput(
                completion.Ok,
                completion.Text,
                completion.Error,
                completion.ElapsedMs,
                completion.RunId,
                model,
                completion.InputTokens,
                completion.OutputTokens,
                completion.TotalTokens)));
        }

        return new AnalyzeResponse(
            results,
            new TimingSummary(rawStopwatch.ElapsedMilliseconds, null, codexTotal, rawStopwatch.ElapsedMilliseconds + codexTotal),
            null);
    }

    public async Task<AnalyzeResponse> AnalyzeComparisonAsync(AnalyzeComparisonRequest request, CancellationToken cancellationToken)
    {
        var rawStopwatch = Stopwatch.StartNew();
        await _recommendationEngine.RecalculateAsync(cancellationToken);
        var candidates = await LoadCandidatesAsync(Math.Clamp(request.Count, 1, 5), cancellationToken);
        rawStopwatch.Stop();

        var ollamaModel = request.OllamaModel?.Trim();
        var results = new List<MovieLabResult>();
        var ollamaTotal = 0L;
        var codexTotal = 0L;
        var codexModels = new[] { "gpt-5.4-mini", "gpt-5.5" };

        foreach (var candidate in candidates)
        {
            OllamaLabOutput? ollama = null;
            if (!string.IsNullOrWhiteSpace(ollamaModel))
            {
                var ollamaCompletion = await _ollamaClient.GenerateAsync(ollamaModel, candidate.OllamaPrompt, request.Temperature, cancellationToken);
                ollamaTotal += ollamaCompletion.ElapsedMs;
                ollama = new OllamaLabOutput(
                    ollamaCompletion.Ok,
                    ollamaCompletion.Text,
                    ollamaCompletion.RawResponse,
                    ollamaCompletion.Error,
                    ollamaCompletion.ElapsedMs);
            }

            var codexRuns = new List<CodexLabOutput>();
            foreach (var codexModel in codexModels)
            {
                var completion = await _codexClient.GenerateAsync(codexModel, BuildCodexDirectPrompt(candidate.Raw), cancellationToken);
                codexTotal += completion.ElapsedMs;
                codexRuns.Add(new CodexLabOutput(
                    completion.Ok,
                    completion.Text,
                    completion.Error,
                    completion.ElapsedMs,
                    completion.RunId,
                    codexModel,
                    completion.InputTokens,
                    completion.OutputTokens,
                    completion.TotalTokens));
            }

            results.Add(new MovieLabResult(candidate, ollama, null, codexRuns));
        }

        return new AnalyzeResponse(
            results,
            new TimingSummary(rawStopwatch.ElapsedMilliseconds, ollamaTotal, codexTotal, rawStopwatch.ElapsedMilliseconds + ollamaTotal + codexTotal),
            string.IsNullOrWhiteSpace(ollamaModel) ? "Ollama was skipped because no Ollama model was selected." : null);
    }

    private async Task<List<MovieLabCandidate>> LoadCandidatesAsync(int count, CancellationToken cancellationToken)
    {
        var preferences = _preferencesService.Get();
        var movies = await _dbContext.Movies
            .AsNoTracking()
            .OrderBy(x => x.Title)
            .ToListAsync(cancellationToken);

        return movies
            .Where(x => MovieStateHelper.IsRecommendationCandidate(x, preferences.DismissScoreThreshold))
            .OrderByDescending(x => x.PredictedScore ?? x.PersonalMatchScore ?? 0m)
            .ThenByDescending(x => x.ImdbRating ?? 0m)
            .ThenBy(x => x.Title)
            .Take(count)
            .Select(BuildCandidate)
            .ToList();
    }

    private static MovieLabCandidate BuildCandidate(Movie movie)
    {
        var context = ParseContext(movie.RecommendationContextJson);
        var raw = new RawMovieDbOutput(
            movie.Id,
            movie.Title,
            movie.Year,
            movie.GenresCsv ?? string.Empty,
            FormatScore(movie.PredictedScore),
            FormatScore(movie.PersonalMatchScore),
            movie.PredictedLabel ?? "Maybe",
            movie.PredictedReason ?? string.Empty,
            movie.ReasonTagsCsv ?? string.Empty,
            FormatScore(movie.ImdbRating),
            movie.Overview ?? string.Empty,
            context?.AffinityScore,
            context?.ConfidenceScore,
            context?.PositiveFactors.Select(FormatFactor).Take(8).ToList() ?? [],
            context?.NegativeFactors.Select(FormatFactor).Take(8).ToList() ?? [],
            context?.WarningFactors.Take(6).ToList() ?? [],
            context?.SimilarToLiked.Take(4).Select(FormatSimilar).ToList() ?? [],
            context?.SimilarToDisliked.Take(4).Select(FormatSimilar).ToList() ?? []);

        var prompt = BuildPrompt(raw, false);
        var codexPrompt = BuildPrompt(raw, true);

        return new MovieLabCandidate(raw, prompt, codexPrompt);
    }

    private static RecommendationContext? ParseContext(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<RecommendationContext>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private static string BuildPrompt(RawMovieDbOutput raw, bool codex)
    {
        var builder = new StringBuilder();
        builder.AppendLine("You are helping test a local movie recommendation app.");
        builder.AppendLine("Use only the evidence below. Do not invent facts about the movie.");
        builder.AppendLine("Return concise structured output with: decision, confidence, explanation, best evidence, caution.");
        if (codex)
        {
            builder.AppendLine("Also say whether the deterministic app explanation should be rewritten and provide a better short explanation.");
        }

        builder.AppendLine();
        builder.AppendLine("Candidate:");
        builder.AppendLine($"Title: {raw.Title} ({raw.Year?.ToString(CultureInfo.InvariantCulture) ?? "unknown year"})");
        builder.AppendLine($"Genres: {raw.Genres}");
        builder.AppendLine($"IMDb: {raw.ImdbRating}");
        builder.AppendLine($"Overview: {Limit(raw.Overview, 700)}");
        builder.AppendLine();
        builder.AppendLine("MovieDb raw judgment:");
        builder.AppendLine($"Predicted score: {raw.PredictedScore}");
        builder.AppendLine($"Taste fit: {raw.PersonalMatchScore}");
        builder.AppendLine($"Label: {raw.PredictedLabel}");
        builder.AppendLine($"Current explanation: {raw.PredictedReason}");
        builder.AppendLine($"Reason tags: {raw.ReasonTags}");
        builder.AppendLine($"Affinity: {raw.AffinityScore?.ToString("0.#", CultureInfo.InvariantCulture) ?? "unknown"}");
        builder.AppendLine($"Confidence: {raw.ConfidenceScore?.ToString("0.#", CultureInfo.InvariantCulture) ?? "unknown"}");
        builder.AppendLine($"Positive factors: {Join(raw.PositiveFactors)}");
        builder.AppendLine($"Negative factors: {Join(raw.NegativeFactors)}");
        builder.AppendLine($"Warnings: {Join(raw.WarningFactors)}");
        builder.AppendLine($"Similar liked anchors: {Join(raw.SimilarLiked)}");
        builder.AppendLine($"Similar disliked anchors: {Join(raw.SimilarDisliked)}");
        builder.AppendLine();
        builder.AppendLine("Decision values must be exactly one of: suggest, maybe, reject.");

        return builder.ToString().Trim();
    }

    private static string BuildCodexDirectPrompt(RawMovieDbOutput raw)
    {
        var prompt = BuildPrompt(raw, true);
        return $"""
{prompt}

Extra constraints for this lab run:
- Do not ask to run commands.
- Do not inspect files.
- Do not suggest changing the app.
- Return only the movie-analysis answer.
- Keep the explanation to 1 short sentence, max 28 words.
- Keep best evidence to 2 bullets max.
- Use confidence as low, medium, or high, not a number.
""";
    }

    private static string FormatScore(decimal? value)
        => value?.ToString("0.#", CultureInfo.InvariantCulture) ?? "";

    private static string FormatFactor(ExplanationFactor factor)
        => $"{factor.Label} ({factor.Weight.ToString("0.#", CultureInfo.InvariantCulture)})";

    private static string FormatSimilar(SimilarMovieSummary movie)
        => $"{movie.Title}: {movie.Verdict}, similarity {movie.SimilarityScore.ToString("0.#", CultureInfo.InvariantCulture)}";

    private static string Join(IReadOnlyList<string> values)
        => values.Count == 0 ? "none" : string.Join("; ", values);

    private static string Limit(string value, int max)
        => value.Length <= max ? value : value[..max].TrimEnd() + "...";
}

public sealed record CandidateListResponse(IReadOnlyList<MovieLabCandidate> Candidates, long RawElapsedMs);

public sealed record AnalyzeResponse(IReadOnlyList<MovieLabResult> Results, TimingSummary Timing, string? Notice);

public sealed record TimingSummary(long RawAppMs, long? OllamaMs, long? CodexMs, long TotalMs);

public sealed record MovieLabResult(MovieLabCandidate Candidate, OllamaLabOutput? Ollama, CodexLabOutput? Codex = null, IReadOnlyList<CodexLabOutput>? CodexRuns = null);

public sealed record MovieLabCandidate(RawMovieDbOutput Raw, string OllamaPrompt, string CodexPrompt);

public sealed record RawMovieDbOutput(
    int Id,
    string Title,
    int? Year,
    string Genres,
    string PredictedScore,
    string PersonalMatchScore,
    string PredictedLabel,
    string PredictedReason,
    string ReasonTags,
    string ImdbRating,
    string Overview,
    decimal? AffinityScore,
    decimal? ConfidenceScore,
    IReadOnlyList<string> PositiveFactors,
    IReadOnlyList<string> NegativeFactors,
    IReadOnlyList<string> WarningFactors,
    IReadOnlyList<string> SimilarLiked,
    IReadOnlyList<string> SimilarDisliked);

public sealed record OllamaLabOutput(bool Ok, string Text, string RawResponse, string? Error, long ElapsedMs);

public sealed record CodexLabOutput(bool Ok, string Text, string? Error, long ElapsedMs, string? RunId, string Model, int? InputTokens, int? OutputTokens, int? TotalTokens);
