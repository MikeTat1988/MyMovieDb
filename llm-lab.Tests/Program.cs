using LocalMovieVault.LlmLab.Services;

await RunStepAsync("AssertCodexCliClientInvokesLocalExecAsync", AssertCodexCliClientInvokesLocalExecAsync);
await RunStepAsync("AssertCodexCliClientReadsTokenUsageAsync", AssertCodexCliClientReadsTokenUsageAsync);
Console.WriteLine("All LocalMovieVault.LlmLab tests passed.");

static async Task RunStepAsync(string name, Func<Task> action)
{
    Console.WriteLine($"RUN {name}");
    await action();
    Console.WriteLine($"PASS {name}");
}

static async Task AssertCodexCliClientInvokesLocalExecAsync()
{
    var workspacePath = FindProjectRoot();
    var executablePath = Path.Combine(Path.GetTempPath(), "codex-test.exe");
    var runner = new CapturingCodexCliRunner("CLI answer");
    var client = new CodexCliClient(
        new CodexCliOptions
        {
            ExecutablePath = executablePath,
            WorkspacePath = workspacePath,
            TimeoutSeconds = 5,
            Models = ["gpt-5.4-mini"]
        },
        runner);

    var result = await client.GenerateAsync("gpt-5.4-mini", "Analyze this movie.", CancellationToken.None);

    if (!result.Ok || result.Text != "CLI answer")
    {
        throw new Exception($"Expected Codex CLI result to read the last-message file, got ok={result.Ok}, text='{result.Text}', error='{result.Error}'.");
    }

    if (runner.Request is null)
    {
        throw new Exception("Expected Codex CLI runner to receive a request.");
    }

    var args = runner.Request.Arguments;
    AssertSequenceContains(args, "exec", "-m", "gpt-5.4-mini", "-C", workspacePath, "--sandbox", "read-only", "--ephemeral");

    if (args.Contains("-a", StringComparer.Ordinal) || args.Contains("--ask-for-approval", StringComparer.Ordinal))
    {
        throw new Exception("Expected Codex CLI request not to include unsupported approval flags for this exec CLI version.");
    }

    if (!string.Equals(runner.Request.FileName, executablePath, StringComparison.Ordinal))
    {
        throw new Exception("Expected Codex CLI executable path to come from lab options.");
    }

    if (!string.Equals(runner.Request.StandardInput, "Analyze this movie.", StringComparison.Ordinal))
    {
        throw new Exception("Expected Codex CLI prompt to be passed through stdin.");
    }

    if (string.IsNullOrWhiteSpace(runner.Request.OutputLastMessagePath) ||
        !args.Contains("--output-last-message", StringComparer.Ordinal) ||
        !string.Equals(args[^1], "-", StringComparison.Ordinal))
    {
        throw new Exception("Expected Codex CLI request to include an output-last-message file.");
    }
}

static async Task AssertCodexCliClientReadsTokenUsageAsync()
{
    var runner = new CapturingCodexCliRunner(
        "CLI answer",
        """
        {"type":"response.completed","usage":{"input_tokens":111,"output_tokens":22,"total_tokens":133}}
        """);
    var client = new CodexCliClient(
        new CodexCliOptions
        {
            ExecutablePath = Path.Combine(Path.GetTempPath(), "codex-test.exe"),
            WorkspacePath = FindProjectRoot(),
            TimeoutSeconds = 5,
            Models = ["gpt-5.4-mini"]
        },
        runner);

    var result = await client.GenerateAsync("gpt-5.4-mini", "Analyze this movie.", CancellationToken.None);

    if (result.InputTokens != 111 || result.OutputTokens != 22 || result.TotalTokens != 133)
    {
        throw new Exception($"Expected parsed token usage 111/22/133, got {result.InputTokens}/{result.OutputTokens}/{result.TotalTokens}.");
    }

    if (runner.Request is null || !runner.Request.Arguments.Contains("--json", StringComparer.Ordinal))
    {
        throw new Exception("Expected Codex CLI request to enable JSONL event output.");
    }
}

static string FindProjectRoot()
{
    var current = new DirectoryInfo(AppContext.BaseDirectory);
    while (current is not null)
    {
        if (File.Exists(Path.Combine(current.FullName, "AGENTS.md")))
        {
            return current.FullName;
        }

        current = current.Parent;
    }

    throw new DirectoryNotFoundException("Could not locate the MovieDb project root.");
}

static void AssertSequenceContains(IReadOnlyList<string> actual, params string[] expected)
{
    for (var index = 0; index <= actual.Count - expected.Length; index++)
    {
        var matched = true;
        for (var offset = 0; offset < expected.Length; offset++)
        {
            if (!string.Equals(actual[index + offset], expected[offset], StringComparison.Ordinal))
            {
                matched = false;
                break;
            }
        }

        if (matched)
        {
            return;
        }
    }

    throw new Exception($"Expected args to contain '{string.Join(" ", expected)}', found '{string.Join(" ", actual)}'.");
}

sealed class CapturingCodexCliRunner : ICodexCliProcessRunner
{
    private readonly string _answer;
    private readonly string _standardOutput;

    public CapturingCodexCliRunner(string answer, string standardOutput = "")
    {
        _answer = answer;
        _standardOutput = standardOutput;
    }

    public CodexCliProcessRequest? Request { get; private set; }

    public Task<CodexCliProcessResult> RunAsync(CodexCliProcessRequest request, TimeSpan timeout, CancellationToken cancellationToken)
    {
        Request = request;
        File.WriteAllText(request.OutputLastMessagePath, _answer);
        return Task.FromResult(new CodexCliProcessResult(0, _standardOutput, string.Empty, false));
    }
}
