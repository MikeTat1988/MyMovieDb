using System.Diagnostics;
using LocalMovieVault.Web.Data;
using LocalMovieVault.Web.Services;
using LocalMovieVault.Web.Services.Recommendations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

// Avoid the default Windows Event Log provider, which can fail in hosted/non-elevated runs.
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

var storageOptions = AppStorageBootstrapper.Initialize(builder.Environment.ContentRootPath, builder.Configuration);
builder.Configuration.AddJsonFile(
    new PhysicalFileProvider(Path.GetDirectoryName(storageOptions.SettingsPath)!),
    Path.GetFileName(storageOptions.SettingsPath),
    optional: true,
    reloadOnChange: true);
builder.Configuration["ConnectionStrings:DefaultConnection"] = $"Data Source={storageOptions.DatabasePath}";

var masterAppPortRaw = Environment.GetEnvironmentVariable("MASTERAPP_PORT");
var masterAppPortIsValid = int.TryParse(masterAppPortRaw, out var masterAppPort) && masterAppPort > 0;

var envConfiguredUrl = Environment.GetEnvironmentVariable("AppHost__Url");
var configuredUrl = masterAppPortIsValid
    ? $"http://127.0.0.1:{masterAppPort}"
    : !string.IsNullOrWhiteSpace(envConfiguredUrl)
        ? envConfiguredUrl
        : builder.Configuration["AppHost:Url"] ?? "http://127.0.0.1:5057";
var bindingUrls = configuredUrl;
builder.WebHost.UseUrls(bindingUrls);

builder.Services.AddControllersWithViews();
builder.Services.AddSingleton(storageOptions);
builder.Services.AddSingleton<AppUserPreferencesService>();
builder.Services.AddScoped<AppEventLogService>();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                      ?? $"Data Source={storageOptions.DatabasePath}";

var dbFilePath = connectionString.Replace("Data Source=", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
var dbDirectory = Path.GetDirectoryName(dbFilePath);
if (!string.IsNullOrWhiteSpace(dbDirectory))
{
    Directory.CreateDirectory(dbDirectory);
}

builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite(connectionString));
builder.Services.AddSingleton<TitleOverrideService>();
builder.Services.AddHttpClient<IMovieMetadataService, OmdbMovieMetadataService>();
builder.Services.AddScoped<MovieUpsertService>();
builder.Services.AddScoped<SeedDataService>();
builder.Services.AddScoped<PersonalMatchService>();
builder.Services.AddScoped<RecommendationPreviewService>();
builder.Services.AddScoped<MetadataBackfillService>();
builder.Services.AddScoped<DocxMovieImportService>();
builder.Services.AddScoped<JsonMovieImportService>();
builder.Services.AddScoped<CsvMovieDeltaImportService>();
builder.Services.AddSingleton<DatabaseSchemaMigrator>();
builder.Services.AddScoped<IPlotKeywordExtractor, PlotKeywordExtractor>();
builder.Services.AddScoped<IRecommendationFeatureExtractor, RecommendationFeatureExtractor>();
builder.Services.AddScoped<IRecommendationExplainer, RecommendationExplainer>();
builder.Services.AddScoped<IRecommendationEngine, DeterministicRecommendationEngine>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var schemaMigrator = scope.ServiceProvider.GetRequiredService<DatabaseSchemaMigrator>();
    await schemaMigrator.MigrateAsync(db);

    var seedService = scope.ServiceProvider.GetRequiredService<SeedDataService>();
    await seedService.SeedAsync();

    var personalMatchService = scope.ServiceProvider.GetRequiredService<PersonalMatchService>();
    await personalMatchService.RecalculateAsync();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseStaticFiles();
app.UseRouting();

app.MapGet("/api/health", () => Results.Ok(new
{
    status = "ok",
    app = "MyMovieDB",
    database = storageOptions.DatabasePath
}));

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

var isMasterAppHosted = string.Equals(Environment.GetEnvironmentVariable("MASTERAPP_HOSTED"), "1", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(Environment.GetEnvironmentVariable("MASTERAPP_HOSTED"), "true", StringComparison.OrdinalIgnoreCase);
var envAutoLaunch = Environment.GetEnvironmentVariable("AppHost__AutoLaunchBrowser");
var autoLaunchBrowser = !isMasterAppHosted && (!string.IsNullOrWhiteSpace(envAutoLaunch)
    ? string.Equals(envAutoLaunch, "true", StringComparison.OrdinalIgnoreCase)
    : app.Configuration.GetValue("AppHost:AutoLaunchBrowser", true));

if (autoLaunchBrowser)
{
    app.Lifetime.ApplicationStarted.Register(() =>
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = configuredUrl,
                UseShellExecute = true
            });
        }
        catch
        {
            // ignored by design
        }
    });
}

app.Run();
