using System.Text.Json;

namespace LocalMovieVault.Web.Services;

public sealed class TitleOverrideService
{
    private readonly string _filePath;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true
    };

    public TitleOverrideService(AppStorageOptions storageOptions)
    {
        _filePath = Path.Combine(storageOptions.DataHomePath, "title-overrides.json");
    }

    public TitleOverride? GetOverride(string? originalTitle)
    {
        if (string.IsNullOrWhiteSpace(originalTitle) || !File.Exists(_filePath))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(_filePath);
            var map = JsonSerializer.Deserialize<Dictionary<string, string>>(json, _jsonOptions);
            if (map is null)
            {
                return null;
            }

            foreach (var pair in map)
            {
                if (!string.Equals(pair.Key?.Trim(), originalTitle.Trim(), StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(pair.Value))
                {
                    continue;
                }

                var raw = pair.Value.Trim();
                if (raw.StartsWith("imdb:", StringComparison.OrdinalIgnoreCase))
                {
                    var imdbId = raw.Substring("imdb:".Length).Trim();
                    if (!string.IsNullOrWhiteSpace(imdbId))
                    {
                        return new TitleOverride(null, imdbId);
                    }
                }

                return new TitleOverride(raw, null);
            }
        }
        catch
        {
            // ignore malformed override file by design
        }

        return null;
    }

    public string? GetOverrideTitle(string? originalTitle) => GetOverride(originalTitle)?.Title;

    public string GetFilePath() => _filePath;
}

public sealed record TitleOverride(string? Title, string? ImdbId);
