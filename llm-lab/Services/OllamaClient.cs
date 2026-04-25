using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;

namespace LocalMovieVault.LlmLab.Services;

public sealed class OllamaClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient;

    public OllamaClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<OllamaModelsResponse> GetModelsAsync(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var response = await _httpClient.GetFromJsonAsync<OllamaTagsResponse>("/api/tags", JsonOptions, cancellationToken);
            stopwatch.Stop();
            return new OllamaModelsResponse(
                true,
                response?.Models.Select(x => x.Name).Order(StringComparer.OrdinalIgnoreCase).ToList() ?? [],
                null,
                stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return new OllamaModelsResponse(false, [], ex.Message, stopwatch.ElapsedMilliseconds);
        }
    }

    public async Task<OllamaCompletionResult> GenerateAsync(string model, string prompt, decimal temperature, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var request = new
            {
                model,
                prompt,
                stream = false,
                options = new
                {
                    temperature = (double)Math.Clamp(temperature, 0m, 1.5m)
                }
            };

            using var response = await _httpClient.PostAsJsonAsync("/api/generate", request, JsonOptions, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            stopwatch.Stop();

            if (!response.IsSuccessStatusCode)
            {
                return new OllamaCompletionResult(false, string.Empty, body, stopwatch.ElapsedMilliseconds, response.StatusCode.ToString());
            }

            var parsed = JsonSerializer.Deserialize<OllamaGenerateResponse>(body, JsonOptions);
            return new OllamaCompletionResult(true, parsed?.Response?.Trim() ?? string.Empty, body, stopwatch.ElapsedMilliseconds, null);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return new OllamaCompletionResult(false, string.Empty, string.Empty, stopwatch.ElapsedMilliseconds, ex.Message);
        }
    }

    private sealed record OllamaTagsResponse(List<OllamaModel> Models);
    private sealed record OllamaModel(string Name);
    private sealed record OllamaGenerateResponse(string? Response);
}

public sealed record OllamaModelsResponse(bool Ok, IReadOnlyList<string> Models, string? Error, long ElapsedMs);

public sealed record OllamaCompletionResult(bool Ok, string Text, string RawResponse, long ElapsedMs, string? Error);
