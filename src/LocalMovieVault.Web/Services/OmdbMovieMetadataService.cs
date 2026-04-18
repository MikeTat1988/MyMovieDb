using System.Globalization;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using LocalMovieVault.Web.Contracts;
using LocalMovieVault.Web.Models;

namespace LocalMovieVault.Web.Services;

public class OmdbMovieMetadataService : IMovieMetadataService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly TitleOverrideService _titleOverrideService;

    public OmdbMovieMetadataService(HttpClient httpClient, IConfiguration configuration, TitleOverrideService titleOverrideService)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _titleOverrideService = titleOverrideService;
    }

    public async Task<MetadataLookupResult> LookupByTitleAsync(string title, int? year, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return new MetadataLookupResult
            {
                Success = false,
                ErrorMessage = "Enter a movie or series title."
            };
        }

        if (!TryGetOmdbSettings(out var baseUrl, out var apiKey, out var settingsError))
        {
            return new MetadataLookupResult { Success = false, ErrorMessage = settingsError };
        }

        var overrideEntry = _titleOverrideService.GetOverride(title);
        if (!string.IsNullOrWhiteSpace(overrideEntry?.ImdbId))
        {
            var byId = await LookupByImdbIdInternalAsync(baseUrl, apiKey, overrideEntry.ImdbId, cancellationToken);
            if (byId.Success)
            {
                return byId;
            }
        }

        var candidates = await SearchCandidatesCoreAsync(baseUrl, apiKey, title, year, 1, cancellationToken);
        if (candidates.Count > 0)
        {
            var chosen = await LookupByImdbIdInternalAsync(baseUrl, apiKey, candidates[0].ImdbId, cancellationToken);
            if (chosen.Success)
            {
                return chosen;
            }
        }

        var titleCandidates = BuildTitleCandidates(title, overrideEntry?.Title).ToList();
        string? lastError = null;

        foreach (var candidate in titleCandidates)
        {
            foreach (var useYear in year.HasValue ? new[] { true, false } : new[] { false })
            {
                var query = $"{baseUrl}?apikey={Uri.EscapeDataString(apiKey)}&plot=short&t={Uri.EscapeDataString(candidate)}";
                if (useYear && year.HasValue)
                {
                    query += $"&y={year.Value}";
                }

                OmdbResponse? response;
                try
                {
                    response = await _httpClient.GetFromJsonAsync<OmdbResponse>(query, cancellationToken);
                }
                catch (Exception ex)
                {
                    return new MetadataLookupResult
                    {
                        Success = false,
                        ErrorMessage = $"Could not reach OMDb: {ex.Message}"
                    };
                }

                if (response is null)
                {
                    lastError = "OMDb returned an empty response.";
                    continue;
                }

                if (string.Equals(response.Response, "True", StringComparison.OrdinalIgnoreCase))
                {
                    var result = MapResponse(response, candidate);
                    await EnrichFromTmdbAsync(result, cancellationToken);
                    return result;
                }

                lastError = response.Error;
            }
        }

        return new MetadataLookupResult
        {
            Success = false,
            ErrorMessage = string.IsNullOrWhiteSpace(lastError)
                ? $"Movie not found in OMDb: {title.Trim()}"
                : $"OMDb: {lastError} - {title.Trim()}"
        };
    }

    public async Task<MetadataLookupResult> LookupByImdbIdAsync(string imdbId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(imdbId))
        {
            return new MetadataLookupResult { Success = false, ErrorMessage = "IMDb ID is missing." };
        }

        if (!TryGetOmdbSettings(out var baseUrl, out var apiKey, out var settingsError))
        {
            return new MetadataLookupResult { Success = false, ErrorMessage = settingsError };
        }

        return await LookupByImdbIdInternalAsync(baseUrl, apiKey, imdbId, cancellationToken);
    }

    public async Task<IReadOnlyList<MetadataSearchCandidate>> SearchCandidatesAsync(string title, int? year, int take = 5, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return Array.Empty<MetadataSearchCandidate>();
        }

        if (!TryGetOmdbSettings(out var baseUrl, out var apiKey, out _))
        {
            return Array.Empty<MetadataSearchCandidate>();
        }

        var overrideEntry = _titleOverrideService.GetOverride(title);
        return await SearchCandidatesCoreAsync(baseUrl, apiKey, overrideEntry?.Title ?? title, year, take, cancellationToken);
    }

    private bool TryGetOmdbSettings(out string baseUrl, out string apiKey, out string error)
    {
        apiKey = _configuration["MetadataProviders:OmDb:ApiKey"] ?? string.Empty;
        baseUrl = _configuration["MetadataProviders:OmDb:BaseUrl"] ?? "https://www.omdbapi.com/";
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            error = "OMDb API key is missing in settings. Manual save still works.";
            return false;
        }

        return true;
    }

    private bool TryGetTmdbSettings(out string apiKey, out string baseUrl)
    {
        apiKey = _configuration["MetadataProviders:TmDb:ApiKey"] ?? string.Empty;
        baseUrl = _configuration["MetadataProviders:TmDb:BaseUrl"] ?? "https://api.themoviedb.org/3";
        return !string.IsNullOrWhiteSpace(apiKey);
    }

    private async Task<IReadOnlyList<MetadataSearchCandidate>> SearchCandidatesCoreAsync(string baseUrl, string apiKey, string title, int? year, int take, CancellationToken cancellationToken)
    {
        var queries = BuildTitleCandidates(title, null).Take(6).ToList();
        var items = new List<SearchItem>();
        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var queryTitle in queries)
        {
            var query = $"{baseUrl}?apikey={Uri.EscapeDataString(apiKey)}&s={Uri.EscapeDataString(queryTitle)}";
            if (year.HasValue)
            {
                query += $"&y={year.Value}";
            }

            SearchResponse? response;
            try
            {
                response = await _httpClient.GetFromJsonAsync<SearchResponse>(query, cancellationToken);
            }
            catch
            {
                continue;
            }

            if (response?.Search is null)
            {
                continue;
            }

            foreach (var item in response.Search)
            {
                if (string.IsNullOrWhiteSpace(item.imdbID) || !seenIds.Add(item.imdbID))
                {
                    continue;
                }

                items.Add(item);
            }

            if (items.Count >= take * 3)
            {
                break;
            }
        }

        var normalizedTitle = NormalizeForMatch(title);
        var ranked = items
            .OrderByDescending(x => NormalizeForMatch(x.Title) == normalizedTitle)
            .ThenByDescending(x => year.HasValue && TryParseYear(x.Year) == year)
            .ThenByDescending(x => !string.Equals(x.Poster, "N/A", StringComparison.OrdinalIgnoreCase))
            .ThenBy(x => Math.Abs((TryParseYear(x.Year) ?? year ?? 0) - (year ?? TryParseYear(x.Year) ?? 0)))
            .ThenBy(x => x.Title)
            .Take(Math.Max(take, 1))
            .ToList();

        var results = new List<MetadataSearchCandidate>();
        foreach (var item in ranked)
        {
            var detail = await LookupByImdbIdInternalAsync(baseUrl, apiKey, item.imdbID!, cancellationToken);
            if (!detail.Success)
            {
                continue;
            }

            results.Add(new MetadataSearchCandidate
            {
                ImdbId = detail.ExternalId ?? item.imdbID!,
                Title = detail.Title,
                OriginalTitle = detail.OriginalTitle,
                Year = detail.Year,
                MediaType = detail.MediaType,
                ImdbRating = detail.ImdbRating,
                RuntimeMinutes = detail.RuntimeMinutes,
                PosterUrl = detail.PosterUrl,
                GenresCsv = detail.GenresCsv
            });
        }

        return results;
    }

    private async Task<MetadataLookupResult> LookupByImdbIdInternalAsync(string baseUrl, string apiKey, string imdbId, CancellationToken cancellationToken)
    {
        var query = $"{baseUrl}?apikey={Uri.EscapeDataString(apiKey)}&plot=short&i={Uri.EscapeDataString(imdbId)}";

        OmdbResponse? response;
        try
        {
            response = await _httpClient.GetFromJsonAsync<OmdbResponse>(query, cancellationToken);
        }
        catch (Exception ex)
        {
            return new MetadataLookupResult
            {
                Success = false,
                ErrorMessage = $"Could not reach OMDb: {ex.Message}"
            };
        }

        if (response is null || !string.Equals(response.Response, "True", StringComparison.OrdinalIgnoreCase))
        {
            return new MetadataLookupResult
            {
                Success = false,
                ErrorMessage = response?.Error
            };
        }

        var mapped = MapResponse(response, imdbId);
        await EnrichFromTmdbAsync(mapped, cancellationToken);
        return mapped;
    }

    private async Task EnrichFromTmdbAsync(MetadataLookupResult result, CancellationToken cancellationToken)
    {
        if (!TryGetTmdbSettings(out var apiKey, out var baseUrl))
        {
            return;
        }

        var searchType = result.MediaType == MediaType.Series ? "tv" : "movie";
        var searchUrl = $"{baseUrl}/search/{searchType}?api_key={Uri.EscapeDataString(apiKey)}&query={Uri.EscapeDataString(result.Title)}";
        if (result.Year.HasValue)
        {
            var yearKey = result.MediaType == MediaType.Series ? "first_air_date_year" : "year";
            searchUrl += $"&{yearKey}={result.Year.Value}";
        }

        TmdbSearchResponse? searchResponse;
        try
        {
            searchResponse = await _httpClient.GetFromJsonAsync<TmdbSearchResponse>(searchUrl, cancellationToken);
        }
        catch
        {
            return;
        }

        var best = searchResponse?.Results?
            .OrderByDescending(x => string.Equals(x.Title ?? x.Name, result.Title, StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(x => MatchYear(x.ReleaseDate ?? x.FirstAirDate, result.Year))
            .FirstOrDefault();

        if (best is null)
        {
            return;
        }

        result.TmdbId = best.Id.ToString(CultureInfo.InvariantCulture);

        var detailsUrl = $"{baseUrl}/{searchType}/{best.Id}?api_key={Uri.EscapeDataString(apiKey)}&append_to_response=keywords,similar,credits";
        TmdbDetailsResponse? details;
        try
        {
            details = await _httpClient.GetFromJsonAsync<TmdbDetailsResponse>(detailsUrl, cancellationToken);
        }
        catch
        {
            return;
        }

        if (details is null)
        {
            return;
        }

        var keywords = (details.Keywords?.Keywords ?? details.Keywords?.Results ?? [])
            .Select(x => x.Name)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(20)
            .ToList();

        var similarTitles = details.Similar?.Results?
            .Select(x => x.Title ?? x.Name)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(12)
            .ToList() ?? [];

        if (keywords.Count > 0)
        {
            result.TmdbKeywordsCsv = string.Join(", ", keywords);
        }

        if (similarTitles.Count > 0)
        {
            result.SimilarTitlesJson = JsonSerializer.Serialize(similarTitles);
        }

        result.Director ??= details.Credits?.Crew?.FirstOrDefault(x => string.Equals(x.Job, "Director", StringComparison.OrdinalIgnoreCase))?.Name;

        var writers = details.Credits?.Crew?
            .Where(x => string.Equals(x.Department, "Writing", StringComparison.OrdinalIgnoreCase))
            .Select(x => x.Name)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .ToList() ?? [];
        if (writers.Count > 0)
        {
            result.Writer ??= string.Join(", ", writers);
        }

        var cast = details.Credits?.Cast?
            .Select(x => x.Name)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .ToList() ?? [];
        if (cast.Count > 0)
        {
            result.Actors ??= string.Join(", ", cast);
        }
    }

    private static IEnumerable<string> BuildTitleCandidates(string rawTitle, string? overrideTitle)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Add(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            var cleaned = Regex.Replace(value.Trim(), @"\s+", " ");
            if (!string.IsNullOrWhiteSpace(cleaned))
            {
                seen.Add(cleaned);
                seen.Add(RemoveDiacritics(cleaned));
            }
        }

        Add(overrideTitle);
        Add(rawTitle);

        var noTrailingYear = Regex.Replace(rawTitle, @"\s*[\(\[]?(19|20)\d{2}[\)\]]?\s*$", string.Empty, RegexOptions.IgnoreCase).Trim();
        Add(noTrailingYear);

        var noDecorators = Regex.Replace(noTrailingYear, @"\((series|mini-series|mini series|tv series|netflix)\)", string.Empty, RegexOptions.IgnoreCase).Trim();
        Add(noDecorators);

        foreach (Match match in Regex.Matches(rawTitle, @"\(([^\)]+)\)"))
        {
            var inner = match.Groups[1].Value.Trim();
            Add(inner);

            var withoutInner = rawTitle.Replace(match.Value, string.Empty, StringComparison.Ordinal).Trim();
            Add(withoutInner);
        }

        foreach (var part in rawTitle.Split(new[] { '/', '|', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            Add(part);
        }

        foreach (var value in seen.ToList())
        {
            Add(value.Replace(":", string.Empty, StringComparison.Ordinal).Trim());
            Add(value.Replace("-", " ", StringComparison.Ordinal).Trim());
            Add(Regex.Replace(value, @"\s+", string.Empty));
            Add(ReplaceArabicRomanNumerals(value));
        }

        return seen;
    }

    private static MetadataLookupResult MapResponse(OmdbResponse response, string fallbackTitle)
    {
        var externalRatingsJson = response.Ratings is { Count: > 0 } ? JsonSerializer.Serialize(response.Ratings) : null;

        return new MetadataLookupResult
        {
            Success = true,
            Title = response.Title ?? fallbackTitle,
            OriginalTitle = response.Title ?? fallbackTitle,
            Year = TryParseYear(response.Year),
            Category = FirstGenre(response.Genre),
            GenresCsv = response.Genre,
            ImdbRating = TryParseDecimal(response.imdbRating),
            ImdbVotes = TryParseInt(response.imdbVotes),
            Metascore = TryParseInt(response.Metascore),
            RuntimeMinutes = TryParseRuntime(response.Runtime),
            ReleasedOn = TryParseDate(response.Released),
            Country = response.Country,
            Language = response.Language,
            Director = response.Director,
            Writer = response.Writer,
            Actors = response.Actors,
            Overview = response.Plot,
            PosterUrl = string.Equals(response.Poster, "N/A", StringComparison.OrdinalIgnoreCase) ? null : response.Poster,
            OmdbType = response.Type,
            OmdbRatingsJson = externalRatingsJson,
            ExternalRatingsJson = externalRatingsJson,
            ExternalId = response.imdbID,
            ExternalSource = "OMDb",
            MediaType = string.Equals(response.Type, "series", StringComparison.OrdinalIgnoreCase) ? MediaType.Series : MediaType.Movie
        };
    }

    private static string NormalizeForMatch(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var normalized = RemoveDiacritics(value).ToLowerInvariant();
        var cleaned = Regex.Replace(normalized, @"[^\p{L}\p{Nd}]+", string.Empty);
        return cleaned.Trim();
    }

    private static string RemoveDiacritics(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return value;
        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();
        foreach (var ch in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(ch);
            }
        }
        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private static string? ReplaceArabicRomanNumerals(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return value;
        var updated = value;
        updated = Regex.Replace(updated, @"\b2\b", "II");
        updated = Regex.Replace(updated, @"\b3\b", "III");
        updated = Regex.Replace(updated, @"\b4\b", "IV");
        updated = Regex.Replace(updated, @"\bII\b", "2", RegexOptions.IgnoreCase);
        updated = Regex.Replace(updated, @"\bIII\b", "3", RegexOptions.IgnoreCase);
        updated = Regex.Replace(updated, @"\bIV\b", "4", RegexOptions.IgnoreCase);
        return updated;
    }

    private static string? FirstGenre(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv)) return null;
        return csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
    }

    private static bool MatchYear(string? date, int? expectedYear)
    {
        if (!expectedYear.HasValue || string.IsNullOrWhiteSpace(date) || date.Length < 4)
        {
            return false;
        }

        return int.TryParse(date[..4], out var year) && year == expectedYear.Value;
    }

    private static int? TryParseYear(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var yearText = value.Split('–', '-', ' ').FirstOrDefault();
        return int.TryParse(yearText, out var year) ? year : null;
    }

    private static int? TryParseRuntime(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var token = value.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return int.TryParse(token, out var runtime) ? runtime : null;
    }

    private static decimal? TryParseDecimal(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || string.Equals(value, "N/A", StringComparison.OrdinalIgnoreCase)) return null;
        return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var result) ? result : null;
    }

    private static int? TryParseInt(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || string.Equals(value, "N/A", StringComparison.OrdinalIgnoreCase)) return null;
        var cleaned = value.Replace(",", string.Empty, StringComparison.Ordinal);
        return int.TryParse(cleaned, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : null;
    }

    private static DateTime? TryParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || string.Equals(value, "N/A", StringComparison.OrdinalIgnoreCase)) return null;
        return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date)
            ? date.Date
            : null;
    }

    private sealed class OmdbResponse
    {
        public string? Title { get; set; }
        public string? Year { get; set; }
        public string? Released { get; set; }
        public string? Runtime { get; set; }
        public string? Genre { get; set; }
        public string? Country { get; set; }
        public string? Language { get; set; }
        public string? Director { get; set; }
        public string? Writer { get; set; }
        public string? Actors { get; set; }
        public string? Plot { get; set; }
        public string? Poster { get; set; }
        public string? imdbRating { get; set; }
        public string? imdbVotes { get; set; }
        public string? Metascore { get; set; }
        public string? imdbID { get; set; }
        public string? Type { get; set; }
        public string? Response { get; set; }
        public string? Error { get; set; }
        public List<OmdbRating>? Ratings { get; set; }
    }

    private sealed class OmdbRating
    {
        public string? Source { get; set; }
        public string? Value { get; set; }
    }

    private sealed class SearchResponse
    {
        public List<SearchItem>? Search { get; set; }
        public string? Response { get; set; }
        public string? Error { get; set; }
    }

    private sealed class SearchItem
    {
        public string Title { get; set; } = string.Empty;
        public string? Year { get; set; }
        public string? imdbID { get; set; }
        public string? Poster { get; set; }
        public string? Type { get; set; }
    }

    private sealed class TmdbSearchResponse
    {
        public List<TmdbSearchItem>? Results { get; set; }
    }

    private sealed class TmdbSearchItem
    {
        public int Id { get; set; }
        public string? Title { get; set; }
        public string? Name { get; set; }
        public string? ReleaseDate { get; set; }
        public string? FirstAirDate { get; set; }
    }

    private sealed class TmdbDetailsResponse
    {
        public TmdbKeywordsContainer? Keywords { get; set; }
        public TmdbSimilarContainer? Similar { get; set; }
        public TmdbCreditsContainer? Credits { get; set; }
    }

    private sealed class TmdbKeywordsContainer
    {
        public List<TmdbNamedItem>? Keywords { get; set; }
        public List<TmdbNamedItem>? Results { get; set; }
    }

    private sealed class TmdbSimilarContainer
    {
        public List<TmdbTitleItem>? Results { get; set; }
    }

    private sealed class TmdbCreditsContainer
    {
        public List<TmdbCrewItem>? Crew { get; set; }
        public List<TmdbCastItem>? Cast { get; set; }
    }

    private sealed class TmdbNamedItem
    {
        public string? Name { get; set; }
    }

    private sealed class TmdbTitleItem
    {
        public string? Title { get; set; }
        public string? Name { get; set; }
    }

    private sealed class TmdbCrewItem
    {
        public string? Name { get; set; }
        public string? Job { get; set; }
        public string? Department { get; set; }
    }

    private sealed class TmdbCastItem
    {
        public string? Name { get; set; }
    }
}
