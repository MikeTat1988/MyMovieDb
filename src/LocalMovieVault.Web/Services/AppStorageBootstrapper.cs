using System.Text.Json;

namespace LocalMovieVault.Web.Services;

public static class AppStorageBootstrapper
{
    public const string DataFolderName = "MyMovieDB";
    public const string DatabaseFileName = "localmovievault.db";
    public const string SettingsFileName = "mymoviedb.settings.json";
    public const string SeedFileName = "seed_movies.json";

    public static AppStorageOptions Initialize(string contentRootPath, IConfiguration localConfiguration)
    {
        var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (string.IsNullOrWhiteSpace(documentsPath))
        {
            throw new InvalidOperationException("Не удалось определить папку Documents для текущего пользователя.");
        }

        var dataHomePath = Path.Combine(documentsPath, DataFolderName);
        Directory.CreateDirectory(dataHomePath);

        var databasePath = Path.Combine(dataHomePath, DatabaseFileName);
        var settingsPath = Path.Combine(dataHomePath, SettingsFileName);
        var seedPath = Path.Combine(dataHomePath, SeedFileName);

        EnsureUserSettingsFile(settingsPath, localConfiguration);
        CopyLegacyDatabaseIfNeeded(databasePath, contentRootPath);
        CopyBundledSeedIfNeeded(seedPath, contentRootPath);

        return new AppStorageOptions
        {
            DataHomePath = dataHomePath,
            DatabasePath = databasePath,
            SettingsPath = settingsPath,
            SeedPath = seedPath
        };
    }

    private static void EnsureUserSettingsFile(string settingsPath, IConfiguration localConfiguration)
    {
        var localApiKey = localConfiguration["MetadataProviders:OmDb:ApiKey"]?.Trim() ?? string.Empty;
        var localBaseUrl = localConfiguration["MetadataProviders:OmDb:BaseUrl"]?.Trim() ?? "https://www.omdbapi.com/";
        var localTmdbApiKey = localConfiguration["MetadataProviders:TmDb:ApiKey"]?.Trim() ?? string.Empty;
        var localTmdbBaseUrl = localConfiguration["MetadataProviders:TmDb:BaseUrl"]?.Trim() ?? "https://api.themoviedb.org/3";
        var localUrl = localConfiguration["AppHost:Url"]?.Trim() ?? "http://127.0.0.1:5057";
        var localAutoLaunch = localConfiguration.GetValue("AppHost:AutoLaunchBrowser", true);

        if (!File.Exists(settingsPath))
        {
            var defaults = new UserSettingsFile
            {
                AppHost = new AppHostSection
                {
                    Url = localUrl,
                    AutoLaunchBrowser = localAutoLaunch
                },
                MetadataProviders = new MetadataProvidersSection
                {
                    OmDb = new OmdbSection
                    {
                        ApiKey = localApiKey,
                        BaseUrl = localBaseUrl
                    },
                    TmDb = new TmdbSection
                    {
                        ApiKey = localTmdbApiKey,
                        BaseUrl = localTmdbBaseUrl
                    }
                }
            };

            WriteSettings(settingsPath, defaults);
            return;
        }

        UserSettingsFile settings;
        try
        {
            var json = File.ReadAllText(settingsPath);
            settings = JsonSerializer.Deserialize<UserSettingsFile>(json, JsonSerializerOptions) ?? new UserSettingsFile();
        }
        catch
        {
            settings = new UserSettingsFile();
        }

        var changed = false;

        settings.AppHost ??= new AppHostSection();
        settings.MetadataProviders ??= new MetadataProvidersSection();
        settings.MetadataProviders.OmDb ??= new OmdbSection();
        settings.MetadataProviders.TmDb ??= new TmdbSection();

        if (string.IsNullOrWhiteSpace(settings.AppHost.Url))
        {
            settings.AppHost.Url = localUrl;
            changed = true;
        }

        if (settings.AppHost.AutoLaunchBrowser is null)
        {
            settings.AppHost.AutoLaunchBrowser = localAutoLaunch;
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(settings.MetadataProviders.OmDb.BaseUrl))
        {
            settings.MetadataProviders.OmDb.BaseUrl = localBaseUrl;
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(settings.MetadataProviders.OmDb.ApiKey) && !string.IsNullOrWhiteSpace(localApiKey))
        {
            settings.MetadataProviders.OmDb.ApiKey = localApiKey;
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(settings.MetadataProviders.TmDb.BaseUrl))
        {
            settings.MetadataProviders.TmDb.BaseUrl = localTmdbBaseUrl;
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(settings.MetadataProviders.TmDb.ApiKey) && !string.IsNullOrWhiteSpace(localTmdbApiKey))
        {
            settings.MetadataProviders.TmDb.ApiKey = localTmdbApiKey;
            changed = true;
        }

        if (changed)
        {
            WriteSettings(settingsPath, settings);
        }
    }

    private static void CopyLegacyDatabaseIfNeeded(string destinationDbPath, string contentRootPath)
    {
        if (File.Exists(destinationDbPath))
        {
            return;
        }

        foreach (var candidate in GetLegacyDatabaseCandidates(contentRootPath))
        {
            if (!File.Exists(candidate) || string.Equals(candidate, destinationDbPath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destinationDbPath)!);
            File.Copy(candidate, destinationDbPath, overwrite: false);

            var wal = candidate + "-wal";
            if (File.Exists(wal))
            {
                File.Copy(wal, destinationDbPath + "-wal", overwrite: true);
            }

            var shm = candidate + "-shm";
            if (File.Exists(shm))
            {
                File.Copy(shm, destinationDbPath + "-shm", overwrite: true);
            }

            return;
        }
    }

    private static IEnumerable<string> GetLegacyDatabaseCandidates(string contentRootPath)
    {
        var candidates = new[]
        {
            Path.Combine(contentRootPath, "App_Data", DatabaseFileName),
            Path.Combine(AppContext.BaseDirectory, "App_Data", DatabaseFileName),
            Path.Combine(Directory.GetCurrentDirectory(), "App_Data", DatabaseFileName),
            Path.Combine(Directory.GetCurrentDirectory(), "src", "LocalMovieVault.Web", "App_Data", DatabaseFileName),
            Path.Combine(AppContext.BaseDirectory, "src", "LocalMovieVault.Web", "App_Data", DatabaseFileName)
        };

        return candidates.Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static void CopyBundledSeedIfNeeded(string destinationSeedPath, string contentRootPath)
    {
        if (File.Exists(destinationSeedPath))
        {
            return;
        }

        var candidates = new[]
        {
            Path.Combine(contentRootPath, "App_Data", "Seed", SeedFileName),
            Path.GetFullPath(Path.Combine(contentRootPath, "..", "..", "..", "source-data", "seed", SeedFileName))
        };

        foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!File.Exists(candidate))
            {
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destinationSeedPath)!);
            File.Copy(candidate, destinationSeedPath, overwrite: false);
            return;
        }
    }

    private static void WriteSettings(string settingsPath, UserSettingsFile settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
        File.WriteAllText(settingsPath, JsonSerializer.Serialize(settings, JsonSerializerOptions));
    }

    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = null
    };

    private sealed class UserSettingsFile
    {
        public AppHostSection? AppHost { get; set; }
        public MetadataProvidersSection? MetadataProviders { get; set; }
    }

    private sealed class AppHostSection
    {
        public string? Url { get; set; }
        public bool? AutoLaunchBrowser { get; set; }
    }

    private sealed class MetadataProvidersSection
    {
        public OmdbSection? OmDb { get; set; }
        public TmdbSection? TmDb { get; set; }
    }

    private sealed class OmdbSection
    {
        public string? ApiKey { get; set; }
        public string? BaseUrl { get; set; }
    }

    private sealed class TmdbSection
    {
        public string? ApiKey { get; set; }
        public string? BaseUrl { get; set; }
    }
}
