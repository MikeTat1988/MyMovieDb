using System.Text.Json;
using System.Text.Json.Nodes;
using LocalMovieVault.Web.Helpers;

namespace LocalMovieVault.Web.Services;

public sealed class AppUserPreferencesService
{
    private const string PreferencesPropertyName = "Preferences";
    private readonly string _settingsPath;
    private readonly JsonSerializerOptions _serializerOptions = new() { WriteIndented = true };

    public AppUserPreferencesService(AppStorageOptions storageOptions)
    {
        _settingsPath = storageOptions.SettingsPath;
    }

    public AppUserPreferences Get()
    {
        var root = LoadRootNode();
        var preferences = LoadPreferences(root?[PreferencesPropertyName] as JsonObject);
        preferences.Normalize();
        return preferences;
    }

    public void Save(AppUserPreferences preferences)
    {
        preferences.Normalize();
        var root = LoadRootNode() ?? new JsonObject();
        root[PreferencesPropertyName] = JsonSerializer.SerializeToNode(preferences, _serializerOptions);
        Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
        File.WriteAllText(_settingsPath, root.ToJsonString(_serializerOptions));
    }

    private AppUserPreferences LoadPreferences(JsonObject? node)
    {
        var preferences = node?.Deserialize<AppUserPreferences>(_serializerOptions) ?? AppUserPreferences.CreateDefault();
        if (preferences.ImportantTags.Count == 0 && node is not null)
        {
            preferences.ImportantTags = LoadLegacyImportantTags(node);
        }

        preferences.TasteTuning ??= TasteTuningSettings.CreateDefault();
        return preferences;
    }

    private JsonObject? LoadRootNode()
    {
        if (!File.Exists(_settingsPath))
        {
            return new JsonObject();
        }

        try
        {
            var json = File.ReadAllText(_settingsPath);
            return JsonNode.Parse(json) as JsonObject ?? new JsonObject();
        }
        catch
        {
            return new JsonObject();
        }
    }

    private static List<string> LoadLegacyImportantTags(JsonObject node)
    {
        var tags = new List<string>();
        for (var index = 1; index <= 4; index++)
        {
            if (node[$"ImportantTag{index}"]?.GetValue<string>() is { } value)
            {
                tags.Add(value);
            }
        }

        return AppUserPreferences.NormalizeImportantTags(tags);
    }
}

public sealed class AppUserPreferences
{
    public string DefaultGenre { get; set; } = "Horror";
    public decimal DismissScoreThreshold { get; set; } = 50m;
    public decimal PredictionMismatchThreshold { get; set; } = 28m;
    public decimal GenrePreferenceWeight { get; set; } = 1.0m;
    public decimal StoryPreferenceWeight { get; set; } = 1.25m;
    public decimal CinematographyPreferenceWeight { get; set; } = 1.2m;
    public decimal ImdbPreferenceWeight { get; set; } = 0.9m;
    public List<string> ImportantTags { get; set; } = [];
    public TasteTuningSettings TasteTuning { get; set; } = TasteTuningSettings.CreateDefault();

    public static AppUserPreferences CreateDefault() => new();

    public IReadOnlyList<string> GetImportantTags()
        => NormalizeImportantTags(ImportantTags);

    public void Normalize()
    {
        ImportantTags = NormalizeImportantTags(ImportantTags);
        TasteTuning ??= TasteTuningSettings.CreateDefault();
    }

    public static List<string> NormalizeImportantTags(IEnumerable<string>? tags)
        => (tags ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(RecommendationViewHelper.CanonicalizeReasonTag)
            .Where(x => RecommendationViewHelper.ReasonTagOptions.Contains(x, StringComparer.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(4)
            .ToList();
}

public sealed class TasteTuningSettings
{
    public int Version { get; set; } = 1;
    public decimal ImportantTagMultiplier { get; set; } = 1.75m;
    public decimal PositiveExplicitTagWeight { get; set; } = 1.0m;
    public decimal GenreAffinityWeight { get; set; } = 1.0m;
    public decimal CreatorAffinityWeight { get; set; } = 1.0m;
    public decimal CrossGenreAnchorWeight { get; set; } = 1.18m;
    public decimal NegativePacingPenaltyWeight { get; set; } = 1.15m;
    public decimal DialogueActingSynergyMultiplier { get; set; } = 1.22m;

    public static TasteTuningSettings CreateDefault() => new();
}
