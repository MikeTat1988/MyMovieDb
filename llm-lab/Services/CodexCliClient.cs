using System.Diagnostics;
using System.Text.Json;

namespace LocalMovieVault.LlmLab.Services;

public sealed class CodexCliClient
{
    private readonly CodexCliOptions _options;
    private readonly ICodexCliProcessRunner _runner;

    public CodexCliClient(CodexCliOptions options, ICodexCliProcessRunner runner)
    {
        _options = options;
        _runner = runner;
    }

    public CodexModelsResponse GetModels()
        => new(true, _options.Models, _options.DefaultModel, null, 0);

    public async Task<CodexCompletionResult> GenerateAsync(string model, string prompt, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var outputPath = Path.Combine(Path.GetTempPath(), $"mymoviedb-codex-{Guid.NewGuid():N}.txt");
        try
        {
            var selectedModel = string.IsNullOrWhiteSpace(model) ? _options.DefaultModel : model.Trim();
            var request = new CodexCliProcessRequest(
                _options.ExecutablePath,
                BuildArguments(selectedModel, _options.WorkspacePath, outputPath),
                prompt,
                _options.WorkspacePath,
                outputPath);

            var result = await _runner.RunAsync(request, TimeSpan.FromSeconds(_options.TimeoutSeconds), cancellationToken);
            stopwatch.Stop();

            if (result.TimedOut)
            {
                return new CodexCompletionResult(false, string.Empty, null, stopwatch.ElapsedMilliseconds, $"Codex CLI timed out after {_options.TimeoutSeconds} seconds.", null, null, null);
            }

            if (result.ExitCode != 0)
            {
                var error = string.IsNullOrWhiteSpace(result.StandardError) ? result.StandardOutput : result.StandardError;
                var failedUsage = ParseUsage(result.StandardOutput);
                return new CodexCompletionResult(false, string.Empty, null, stopwatch.ElapsedMilliseconds, error.Trim(), failedUsage.InputTokens, failedUsage.OutputTokens, failedUsage.TotalTokens);
            }

            var text = File.Exists(outputPath)
                ? await File.ReadAllTextAsync(outputPath, cancellationToken)
                : result.StandardOutput;
            var usage = ParseUsage(result.StandardOutput);
            return new CodexCompletionResult(true, text.Trim(), null, stopwatch.ElapsedMilliseconds, null, usage.InputTokens, usage.OutputTokens, usage.TotalTokens);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return new CodexCompletionResult(false, string.Empty, null, stopwatch.ElapsedMilliseconds, ex.Message, null, null, null);
        }
        finally
        {
            TryDelete(outputPath);
        }
    }

    private static IReadOnlyList<string> BuildArguments(string model, string workspacePath, string outputPath)
        =>
        [
            "exec",
            "-m",
            model,
            "-C",
            workspacePath,
            "--sandbox",
            "read-only",
            "--ephemeral",
            "--json",
            "--output-last-message",
            outputPath,
            "-"
        ];

    private static CodexTokenUsage ParseUsage(string stdout)
    {
        CodexTokenUsage usage = new(null, null, null);
        foreach (var line in stdout.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                using var document = JsonDocument.Parse(line);
                var candidate = FindUsage(document.RootElement);
                if (candidate.HasAny)
                {
                    usage = new(
                        candidate.InputTokens ?? usage.InputTokens,
                        candidate.OutputTokens ?? usage.OutputTokens,
                        candidate.TotalTokens ?? usage.TotalTokens);
                }
            }
            catch
            {
            }
        }

        return usage;
    }

    private static CodexTokenUsage FindUsage(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            CodexTokenUsage direct = new(null, null, null);
            foreach (var property in element.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.Number &&
                    property.Value.TryGetInt32(out var value))
                {
                    direct = property.Name switch
                    {
                        "input_tokens" or "inputTokens" or "prompt_tokens" or "promptTokens" => direct with { InputTokens = value },
                        "output_tokens" or "outputTokens" or "completion_tokens" or "completionTokens" => direct with { OutputTokens = value },
                        "total_tokens" or "totalTokens" => direct with { TotalTokens = value },
                        _ => direct
                    };
                }
            }

            if (direct.HasAny)
            {
                return direct;
            }

            foreach (var property in element.EnumerateObject())
            {
                var nested = FindUsage(property.Value);
                if (nested.HasAny)
                {
                    return nested;
                }
            }
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                var nested = FindUsage(item);
                if (nested.HasAny)
                {
                    return nested;
                }
            }
        }

        return new CodexTokenUsage(null, null, null);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }
}

public sealed class CodexCliOptions
{
    public string ExecutablePath { get; set; } = "codex";
    public string WorkspacePath { get; set; } = Directory.GetCurrentDirectory();
    public int TimeoutSeconds { get; set; } = 300;
    public string DefaultModel { get; set; } = "gpt-5.4-mini";
    public IReadOnlyList<string> Models { get; set; } =
    [
        "gpt-5.4-mini",
        "gpt-5.3-codex",
        "gpt-5.2",
        "gpt-5.4",
        "gpt-5.5"
    ];
}

public interface ICodexCliProcessRunner
{
    Task<CodexCliProcessResult> RunAsync(CodexCliProcessRequest request, TimeSpan timeout, CancellationToken cancellationToken);
}

public sealed class CodexCliProcessRunner : ICodexCliProcessRunner
{
    public async Task<CodexCliProcessResult> RunAsync(CodexCliProcessRequest request, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var process = new Process();
        process.StartInfo.FileName = request.FileName;
        process.StartInfo.WorkingDirectory = request.WorkingDirectory;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardInput = true;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.CreateNoWindow = true;
        foreach (var argument in request.Arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        process.Start();
        await process.StandardInput.WriteAsync(request.StandardInput);
        process.StandardInput.Close();

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        var exited = await WaitForExitAsync(process, timeout, cancellationToken);
        if (!exited)
        {
            TryKill(process);
        }

        var stdout = await ReadCompletedAsync(stdoutTask);
        var stderr = await ReadCompletedAsync(stderrTask);
        return new CodexCliProcessResult(exited ? process.ExitCode : null, stdout, stderr, !exited);
    }

    private static async Task<bool> WaitForExitAsync(Process process, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var exitTask = process.WaitForExitAsync(cancellationToken);
        var completed = await Task.WhenAny(exitTask, Task.Delay(timeout, cancellationToken));
        return completed == exitTask;
    }

    private static async Task<string> ReadCompletedAsync(Task<string> task)
    {
        try
        {
            return await task;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
        }
    }
}

public sealed record CodexCliProcessRequest(
    string FileName,
    IReadOnlyList<string> Arguments,
    string StandardInput,
    string WorkingDirectory,
    string OutputLastMessagePath);

public sealed record CodexCliProcessResult(int? ExitCode, string StandardOutput, string StandardError, bool TimedOut);

public sealed record CodexModelsResponse(bool Ok, IReadOnlyList<string> Models, string? CurrentModel, string? Error, long ElapsedMs);

public sealed record CodexCompletionResult(bool Ok, string Text, string? RunId, long ElapsedMs, string? Error, int? InputTokens, int? OutputTokens, int? TotalTokens);

public sealed record CodexTokenUsage(int? InputTokens, int? OutputTokens, int? TotalTokens)
{
    public bool HasAny => InputTokens.HasValue || OutputTokens.HasValue || TotalTokens.HasValue;
}
