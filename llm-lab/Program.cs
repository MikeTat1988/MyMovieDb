using LocalMovieVault.LlmLab.Services;
using LocalMovieVault.Web.Data;
using LocalMovieVault.Web.Services;
using LocalMovieVault.Web.Services.Recommendations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

var webProjectRoot = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "src", "LocalMovieVault.Web"));
var storageOptions = AppStorageBootstrapper.Initialize(webProjectRoot, builder.Configuration);
builder.Configuration.AddJsonFile(
    new PhysicalFileProvider(Path.GetDirectoryName(storageOptions.SettingsPath)!),
    Path.GetFileName(storageOptions.SettingsPath),
    optional: true,
    reloadOnChange: true);

var connectionString = $"Data Source={storageOptions.DatabasePath}";

builder.Services.AddSingleton(storageOptions);
builder.Services.AddSingleton<AppUserPreferencesService>();
builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite(connectionString));
builder.Services.AddSingleton<DatabaseSchemaMigrator>();
builder.Services.AddScoped<IPlotKeywordExtractor, PlotKeywordExtractor>();
builder.Services.AddScoped<IRecommendationFeatureExtractor, RecommendationFeatureExtractor>();
builder.Services.AddScoped<IRecommendationExplainer, RecommendationExplainer>();
builder.Services.AddScoped<IRecommendationEngine, DeterministicRecommendationEngine>();
builder.Services.AddScoped<LabRecommendationService>();
builder.Services.AddHttpClient<OllamaClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Ollama:BaseUrl"] ?? "http://127.0.0.1:11434");
    client.Timeout = TimeSpan.FromMinutes(8);
});
builder.Services.AddSingleton(CreateCodexCliOptions(builder));
builder.Services.AddSingleton<ICodexCliProcessRunner, CodexCliProcessRunner>();
builder.Services.AddSingleton<CodexCliClient>();

var configuredUrl = builder.Configuration["LabHost:Url"] ?? "http://127.0.0.1:5099";
builder.WebHost.UseUrls(configuredUrl);

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var migrator = scope.ServiceProvider.GetRequiredService<DatabaseSchemaMigrator>();
    await migrator.MigrateAsync(db);
}

app.MapGet("/api/health", (AppStorageOptions storage) => Results.Ok(new
{
    status = "ok",
    lab = "MovieDb LLM Lab",
    database = storage.DatabasePath
}));

app.MapGet("/api/models", async (OllamaClient ollama, CancellationToken cancellationToken) =>
{
    var result = await ollama.GetModelsAsync(cancellationToken);
    return Results.Ok(result);
});

app.MapGet("/api/candidates", async (int? count, LabRecommendationService lab, CancellationToken cancellationToken) =>
{
    var result = await lab.GetCandidatesAsync(Math.Clamp(count ?? 5, 1, 20), cancellationToken);
    return Results.Ok(result);
});

app.MapPost("/api/analyze", async (AnalyzeRequest request, LabRecommendationService lab, CancellationToken cancellationToken) =>
{
    var result = await lab.AnalyzeAsync(request, cancellationToken);
    return Results.Ok(result);
});

app.MapGet("/api/codex/models", (CodexCliClient codex) =>
{
    var result = codex.GetModels();
    return Results.Ok(result);
});

app.MapPost("/api/codex/analyze", async (AnalyzeRequest request, LabRecommendationService lab, CancellationToken cancellationToken) =>
{
    var result = await lab.AnalyzeWithCodexAsync(request, cancellationToken);
    return Results.Ok(result);
});

app.MapPost("/api/compare", async (AnalyzeComparisonRequest request, LabRecommendationService lab, CancellationToken cancellationToken) =>
{
    var result = await lab.AnalyzeComparisonAsync(request, cancellationToken);
    return Results.Ok(result);
});

app.MapPost("/api/reports/last-run", async (SaveReportRequest request, IWebHostEnvironment environment, CancellationToken cancellationToken) =>
{
    var reportsDirectory = Path.Combine(environment.ContentRootPath, "reports");
    Directory.CreateDirectory(reportsDirectory);

    var jsonPath = Path.Combine(reportsDirectory, "last-run.json");
    var markdownPath = Path.Combine(reportsDirectory, "last-run.md");

    var payload = JsonSerializer.Deserialize<JsonElement>(request.Payload.GetRawText());
    var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
    await File.WriteAllTextAsync(jsonPath, json, cancellationToken);
    await File.WriteAllTextAsync(markdownPath, request.Markdown ?? "# MovieDb LLM Lab Report", cancellationToken);

    return Results.Ok(new
    {
        ok = true,
        jsonPath,
        markdownPath
    });
});

app.MapPost("/api/taste-calibration/save", async (SaveTasteCalibrationRequest request, IWebHostEnvironment environment, CancellationToken cancellationToken) =>
{
    var reportsDirectory = Path.Combine(environment.ContentRootPath, "reports");
    Directory.CreateDirectory(reportsDirectory);

    var jsonPath = Path.Combine(reportsDirectory, "taste-calibration-survey-results.json");
    var textPath = Path.Combine(reportsDirectory, "taste-calibration-survey-results.txt");

    var payload = JsonSerializer.Deserialize<JsonElement>(request.Payload.GetRawText());
    var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
    await File.WriteAllTextAsync(jsonPath, json, cancellationToken);
    await File.WriteAllTextAsync(textPath, request.Text ?? "MovieDb Taste Calibration Survey", cancellationToken);

    return Results.Ok(new
    {
        ok = true,
        jsonPath,
        textPath
    });
});

app.Run();

static CodexCliOptions CreateCodexCliOptions(WebApplicationBuilder builder)
{
    var options = new CodexCliOptions
    {
        WorkspacePath = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, ".."))
    };
    builder.Configuration.GetSection("CodexCli").Bind(options);

    var environmentPath = Environment.GetEnvironmentVariable("CODEX_CLI_PATH");
    if (!string.IsNullOrWhiteSpace(environmentPath))
    {
        options.ExecutablePath = environmentPath;
    }
    else if (string.Equals(options.ExecutablePath, "codex", StringComparison.OrdinalIgnoreCase))
    {
        var localPackagePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Packages",
            "OpenAI.Codex_2p2nqsd0c76g0",
            "LocalCache",
            "Local",
            "OpenAI",
            "Codex",
            "bin",
            "codex.exe");
        if (File.Exists(localPackagePath))
        {
            options.ExecutablePath = localPackagePath;
        }
    }

    return options;
}

public sealed record AnalyzeRequest(int Count, string? Model, decimal Temperature);

public sealed record AnalyzeComparisonRequest(int Count, string? OllamaModel, decimal Temperature);

public sealed record SaveReportRequest(JsonElement Payload, string? Markdown);

public sealed record SaveTasteCalibrationRequest(JsonElement Payload, string? Text);
