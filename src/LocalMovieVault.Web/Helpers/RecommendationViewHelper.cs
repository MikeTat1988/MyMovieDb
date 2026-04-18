using LocalMovieVault.Web.Models;

namespace LocalMovieVault.Web.Helpers;

public static class RecommendationViewHelper
{
    public const int MaxReasonTags = 6;
    public const int StandardMinReasonTags = 3;
    public const int AbandonedMinReasonTags = 0;

    public sealed record ReasonTagDefinition(string Key, string Label, string Group, bool IsPositive, IReadOnlyList<string>? Genres = null);

    public static readonly IReadOnlyList<(PersonalVerdict Verdict, string Label)> VerdictOptions =
    [
        (PersonalVerdict.Loved, "Loved"),
        (PersonalVerdict.Liked, "Liked"),
        (PersonalVerdict.Okay, "Meh"),
        (PersonalVerdict.AvoidType, "Couldn't finish")
    ];

    public static readonly IReadOnlyList<(UserGrade Grade, string Label)> GradeOptions =
    [
        (UserGrade.Loved, "Loved"),
        (UserGrade.Liked, "Liked"),
        (UserGrade.Meh, "Meh"),
        (UserGrade.CouldntFinish, "Couldn't finish")
    ];

    public static readonly IReadOnlyList<ReasonTagDefinition> ReasonTagDefinitions =
    [
        new("original_idea", "Original idea", "Story", true),
        new("thought_provoking", "Thought-provoking", "Story", true),
        new("great_twist", "Great twist", "Story", true),
        new("weak_twist", "Weak twist", "Story", false),
        new("strong_ending", "Strong ending", "Story", true),
        new("weak_ending", "Weak ending", "Story", false),
        new("great_acting", "Great acting", "Craft", true),
        new("weak_acting", "Weak acting", "Craft", false),
        new("great_dialogue", "Great dialogue", "Craft", true),
        new("weak_dialogue", "Weak dialogue", "Craft", false),
        new("incredible_visuals", "Incredible visuals", "Craft", true),
        new("weak_visuals", "Weak visuals", "Craft", false),
        new("immersive", "Immersive", "Experience", true),
        new("breaks_immersion", "Breaks immersion", "Experience", false),
        new("good_pacing", "Good pacing", "Experience", true),
        new("too_slow", "Too slow", "Experience", false),
        new("scary", "Scary", "Genre vibe", true, ["Horror", "Thriller"]),
        new("not_scary", "Not scary", "Genre vibe", false, ["Horror", "Thriller"]),
        new("tense", "Tense", "Genre vibe", true, ["Horror", "Thriller", "Mystery", "Crime"]),
        new("no_tension", "No tension", "Genre vibe", false, ["Horror", "Thriller", "Mystery", "Crime"]),
        new("disturbing_atmosphere", "Disturbing atmosphere", "Genre vibe", true, ["Horror", "Thriller"]),
        new("flat_atmosphere", "Flat atmosphere", "Genre vibe", false, ["Horror", "Thriller"]),
        new("funny", "Funny", "Genre vibe", true, ["Comedy"]),
        new("not_funny", "Not funny", "Genre vibe", false, ["Comedy"]),
        new("charming", "Charming", "Genre vibe", true, ["Comedy", "Romance"]),
        new("cringe_humor", "Cringe humor", "Genre vibe", false, ["Comedy"]),
        new("exciting_action", "Exciting action", "Genre vibe", true, ["Action", "Adventure", "Sci-Fi"]),
        new("weak_action", "Weak action", "Genre vibe", false, ["Action", "Adventure", "Sci-Fi"]),
        new("epic_scale", "Epic scale", "Genre vibe", true, ["Action", "Adventure", "Sci-Fi", "Fantasy"]),
        new("great_worldbuilding", "Great worldbuilding", "Genre vibe", true, ["Action", "Adventure", "Sci-Fi", "Fantasy"]),
        new("weak_worldbuilding", "Weak worldbuilding", "Genre vibe", false, ["Action", "Adventure", "Sci-Fi", "Fantasy"]),
        new("emotional", "Emotional", "Genre vibe", true, ["Drama", "Romance"]),
        new("strong_chemistry", "Strong chemistry", "Genre vibe", true, ["Drama", "Romance"]),
        new("weak_chemistry", "Weak chemistry", "Genre vibe", false, ["Drama", "Romance"]),
        new("too_long", "Too long", "More", false),
        new("attention_to_detail", "Attention to detail", "More", true),
        new("comfort_watch", "Comfort watch", "More", true),
        new("believable_characters", "Believable characters", "More", true),
        new("annoying_characters", "Annoying characters", "More", false),
        new("confusing", "Confusing", "More", false),
        new("weird_in_a_good_way", "Weird in a good way", "More", true)
    ];

    public static readonly IReadOnlyList<string> ReasonTagOptions = ReasonTagDefinitions.Select(x => x.Label).ToList();

    private static readonly IReadOnlyDictionary<string, string> ReasonTagAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Thoughtful plot"] = "Thought-provoking",
        ["Beautiful visuals"] = "Incredible visuals",
        ["Extreme tension"] = "Tense",
        ["Intense horror"] = "Scary",
        ["Too dumb / broke immersion"] = "Breaks immersion",
        ["Too dumb / immersion-breaking"] = "Breaks immersion"
    };

    private static readonly HashSet<string> AmbiguousLegacyTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "Acting",
        "Atmosphere",
        "Good atmosphere",
        "Great story",
        "Twist",
        "Unexpected ending",
        "Slow burn done right",
        "Wacky script"
    };

    private static readonly IReadOnlyDictionary<string, string> ReasonTagLabelByKey =
        ReasonTagDefinitions.ToDictionary(x => x.Key, x => x.Label, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<string> ImportantTagOptions =>
    [
        "Original idea",
        "Thought-provoking",
        "Attention to detail",
        "Great twist",
        "Incredible visuals",
        "Immersive",
        "Disturbing atmosphere",
        "Tense",
        "Scary"
    ];

    public static IReadOnlyList<string> PositiveTagOptions =>
        ReasonTagDefinitions.Where(x => x.IsPositive).Select(x => x.Label).ToList();

    public static IReadOnlyList<string> NegativeTagOptions =>
        ReasonTagDefinitions.Where(x => !x.IsPositive).Select(x => x.Label).ToList();

    public static decimal GetDisplayMatchScore(Movie movie)
        => movie.PredictedScore ?? movie.PersonalMatchScore ?? 0m;

    public static IReadOnlyList<ReasonTagDefinition> GetGeneralReasonTagDefinitions()
        => ReasonTagDefinitions.Where(x => x.Genres is null || x.Genres.Count == 0).ToList();

    public static IReadOnlyList<ReasonTagDefinition> GetGenreSpecificReasonTagDefinitions(string? genresCsv)
    {
        var genres = SplitCsv(genresCsv).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (genres.Count == 0)
        {
            return [];
        }

        return ReasonTagDefinitions
            .Where(x => x.Genres is not null && x.Genres.Count > 0 && x.Genres.Any(genres.Contains))
            .ToList();
    }

    public static decimal MapVerdictToUserRating(PersonalVerdict verdict) => MapGradeToUserRating(MapVerdictToGrade(verdict));

    public static decimal MapGradeToUserRating(UserGrade grade)
        => grade switch
        {
            UserGrade.Loved => 95m,
            UserGrade.Liked => 80m,
            UserGrade.Meh => 55m,
            UserGrade.CouldntFinish => 20m,
            _ => 55m
        };

    public static UserGrade? MapScoreToGrade(decimal? score)
    {
        if (!score.HasValue)
        {
            return null;
        }

        return score.Value switch
        {
            >= 90m => UserGrade.Loved,
            >= 75m => UserGrade.Liked,
            >= 40m => UserGrade.Meh,
            _ => UserGrade.CouldntFinish
        };
    }

    public static UserGrade? ParseGrade(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        foreach (var option in GradeOptions)
        {
            if (string.Equals(option.Label, value.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return option.Grade;
            }
        }

        return Enum.TryParse<UserGrade>(value, true, out var grade) ? grade : null;
    }

    public static string GetGradeLabel(UserGrade? grade)
    {
        if (!grade.HasValue)
        {
            return "Not graded";
        }

        return GradeOptions.FirstOrDefault(x => x.Grade == grade.Value).Label ?? grade.Value.ToString();
    }

    public static PersonalVerdict? MapGradeToVerdict(UserGrade? grade)
        => grade switch
        {
            UserGrade.Loved => PersonalVerdict.Loved,
            UserGrade.Liked => PersonalVerdict.Liked,
            UserGrade.Meh => PersonalVerdict.Okay,
            UserGrade.CouldntFinish => PersonalVerdict.AvoidType,
            _ => null
        };

    public static UserGrade MapVerdictToGrade(PersonalVerdict verdict)
        => verdict switch
        {
            PersonalVerdict.Loved => UserGrade.Loved,
            PersonalVerdict.Liked => UserGrade.Liked,
            PersonalVerdict.Okay => UserGrade.Meh,
            PersonalVerdict.DidNotLike => UserGrade.Meh,
            PersonalVerdict.AvoidType => UserGrade.CouldntFinish,
            _ => UserGrade.Meh
        };

    public static int GetMinimumReasonTags(UserGrade? grade)
        => grade == UserGrade.CouldntFinish ? AbandonedMinReasonTags : StandardMinReasonTags;

    public static string CanonicalizeReasonTag(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return string.Empty;
        }

        var trimmed = tag.Trim();
        if (ReasonTagAliases.TryGetValue(trimmed, out var alias))
        {
            trimmed = alias;
        }

        var exact = ReasonTagDefinitions.FirstOrDefault(x => string.Equals(x.Label, trimmed, StringComparison.OrdinalIgnoreCase));
        return exact?.Label ?? trimmed;
    }

    public static bool IsAmbiguousLegacyTag(string? tag)
        => !string.IsNullOrWhiteSpace(tag) && AmbiguousLegacyTags.Contains(tag.Trim());

    public static IReadOnlyList<string> NormalizeImportedTags(string? csv)
    {
        var tags = SplitCsv(csv)
            .Select(CanonicalizeReasonTag)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Where(x => ReasonTagOptions.Contains(x, StringComparer.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return tags;
    }

    public static IReadOnlyList<string> MigrateLegacyTags(string? csv)
    {
        return SplitCsv(csv)
            .Where(x => !IsAmbiguousLegacyTag(x))
            .Select(CanonicalizeReasonTag)
            .Where(x => ReasonTagOptions.Contains(x, StringComparer.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static string GetReasonTagKey(string label)
    {
        var canonical = CanonicalizeReasonTag(label);
        var definition = ReasonTagDefinitions.FirstOrDefault(x => string.Equals(x.Label, canonical, StringComparison.OrdinalIgnoreCase));
        if (definition is not null)
        {
            return definition.Key;
        }

        return canonical
            .ToLowerInvariant()
            .Replace(" ", "_")
            .Replace("-", "_");
    }

    public static string GetReasonTagLabelFromKey(string key)
        => ReasonTagLabelByKey.TryGetValue(key, out var label) ? label : key;

    public static decimal GetBaseReasonTagWeight(string label)
    {
        var canonical = CanonicalizeReasonTag(label);
        return canonical switch
        {
            "Original idea" => 1.35m,
            "Thought-provoking" => 1.35m,
            "Attention to detail" => 1.35m,
            "Great twist" => 1.25m,
            "Incredible visuals" => 1.2m,
            "Immersive" => 1.2m,
            "Disturbing atmosphere" => 1.25m,
            "Tense" => 1.25m,
            "Scary" => 1.2m,
            "Breaks immersion" => 1.15m,
            _ => 1.0m
        };
    }

    public static string GetVerdictLabel(PersonalVerdict? verdict)
    {
        if (!verdict.HasValue)
        {
            return "Not rated";
        }

        foreach (var option in VerdictOptions)
        {
            if (option.Verdict == verdict.Value)
            {
                return option.Label;
            }
        }

        return verdict.Value.ToString();
    }

    public static IEnumerable<string> SplitCsv(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv))
        {
            return Enumerable.Empty<string>();
        }

        return csv
            .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(CanonicalizeReasonTag)
            .Where(x => !string.IsNullOrWhiteSpace(x));
    }

    public static string JoinCsv(IEnumerable<string> values)
    {
        var distinct = values
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(CanonicalizeReasonTag)
            .Where(x => ReasonTagOptions.Contains(x, StringComparer.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return distinct.Count == 0 ? string.Empty : string.Join(", ", distinct);
    }
}
