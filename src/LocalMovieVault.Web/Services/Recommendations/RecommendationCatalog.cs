using System.Globalization;
using System.Text.RegularExpressions;
using LocalMovieVault.Web.Helpers;
using LocalMovieVault.Web.Models;

namespace LocalMovieVault.Web.Services.Recommendations;

public static class RecommendationCatalog
{
    private static readonly Dictionary<UserGrade, int> GradeScores = new()
    {
        [UserGrade.Loved] = 2,
        [UserGrade.Liked] = 1,
        [UserGrade.Meh] = -1,
        [UserGrade.CouldntFinish] = -2
    };

    private static readonly Dictionary<string, string[]> ReasonTagKeywordHints = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Thought-provoking"] = ["story", "narrative", "script", "well-written", "thoughtful", "smart", "ideas", "existential", "philosophical"],
        ["Attention to detail"] = ["detail", "detailed", "carefully built", "meticulous"],
        ["Immersive"] = ["immersive", "absorbing", "transporting"],
        ["Breaks immersion"] = ["immersion-breaking", "breaks immersion", "ridiculous", "goofy"],
        ["Original idea"] = ["original", "inventive", "fresh", "unusual", "surreal"],
        ["Strong ending"] = ["ending", "finale", "payoff", "resolution", "climax"],
        ["Great twist"] = ["twist", "reveal", "turn", "turns", "shocking"],
        ["Weak twist"] = ["twist", "reveal", "predictable"],
        ["Good pacing"] = ["fast-paced", "pace", "relentless", "tight"],
        ["Too slow"] = ["slow", "slow-burn", "patient", "measured"],
        ["Too long"] = ["long", "overlong", "bloated", "drags"],
        ["Confusing"] = ["confusing", "hard to follow", "incomprehensible", "messy"],
        ["Great acting"] = ["actor", "performance", "performances", "cast"],
        ["Weak acting"] = ["wooden", "stilted", "bad acting", "weak performance"],
        ["Believable characters"] = ["family", "character", "relationship", "human"],
        ["Annoying characters"] = ["annoying", "unlikable", "insufferable", "grating"],
        ["Incredible visuals"] = ["visual", "striking", "cinematography", "shot", "shots", "gorgeous", "stunning", "spectacle"],
        ["Weak visuals"] = ["flat visuals", "cheap-looking", "ugly"],
        ["Disturbing atmosphere"] = ["atmosphere", "atmospheric", "eerie", "haunting", "moody", "dread", "disturbing"],
        ["Flat atmosphere"] = ["flat atmosphere", "lifeless", "bland"],
        ["Funny"] = ["funny", "hilarious", "comedy", "laugh"],
        ["Not funny"] = ["unfunny", "not funny", "humorless"],
        ["Charming"] = ["charming", "warm", "sweet"],
        ["Cringe humor"] = ["cringe", "awkward humor", "forced jokes"],
        ["Emotional"] = ["emotional", "grief", "love", "loss", "heartbreaking"],
        ["Tense"] = ["tense", "suspense", "thriller", "pressure", "relentless"],
        ["No tension"] = ["low tension", "flat suspense"],
        ["Scary"] = ["horror", "terrifying", "fear", "monster", "demonic", "nightmare"],
        ["Not scary"] = ["not scary", "mild horror"],
        ["Comfort watch"] = ["comfort", "cozy", "warm", "feel-good"],
        ["Great dialogue"] = ["dialogue", "banter", "sharp writing"],
        ["Weak dialogue"] = ["bad dialogue", "clunky dialogue", "stilted dialogue"],
        ["Exciting action"] = ["action", "set piece", "chase", "explosive"],
        ["Weak action"] = ["flat action", "weak action"],
        ["Epic scale"] = ["epic", "massive", "grand scale"],
        ["Great worldbuilding"] = ["worldbuilding", "lore", "setting"],
        ["Weak worldbuilding"] = ["thin worldbuilding", "generic setting"],
        ["Strong chemistry"] = ["chemistry", "romance", "connection"],
        ["Weak chemistry"] = ["weak chemistry", "no chemistry"],
        ["Weird in a good way"] = ["weird", "odd", "offbeat"]
    };

    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "and", "for", "with", "that", "this", "from", "into", "their", "about", "after", "before",
        "while", "where", "when", "over", "under", "through", "during", "have", "has", "had", "been", "will",
        "would", "could", "should", "than", "them", "they", "there", "what", "your", "you", "his", "her", "she",
        "him", "our", "out", "not", "who", "whose", "which", "also", "just", "make", "made", "movie", "film"
    };

    public static int GetGradeWeight(UserGrade grade) => GradeScores[grade];

    public static PersonalVerdict? MapLegacyVerdict(Movie movie)
    {
        if (movie.PrimaryVerdict.HasValue)
        {
            return movie.PrimaryVerdict;
        }

        if (movie.WatchedStatus != WatchedStatus.Watched)
        {
            return null;
        }

        if (movie.UserRating is >= 85m)
        {
            return PersonalVerdict.Loved;
        }

        if (movie.UserRating is >= 70m)
        {
            return PersonalVerdict.Liked;
        }

        if (movie.UserRating is >= 55m)
        {
            return PersonalVerdict.Okay;
        }

        if (movie.UserRating is >= 35m)
        {
            return PersonalVerdict.DidNotLike;
        }

        if (movie.UserRating is > 0)
        {
            return PersonalVerdict.AvoidType;
        }

        var notes = (movie.Notes ?? string.Empty).ToLowerInvariant();
        if (notes.Contains("favorite") || notes.Contains("loved"))
        {
            return PersonalVerdict.Loved;
        }

        if (notes.Contains("liked") || notes.Contains("great"))
        {
            return PersonalVerdict.Liked;
        }

        if (notes.Contains("meh") || notes.Contains("boring") || notes.Contains("weak"))
        {
            return PersonalVerdict.DidNotLike;
        }

        if (notes.Contains("avoid") || notes.Contains("hate"))
        {
            return PersonalVerdict.AvoidType;
        }
        return PersonalVerdict.Okay;
    }

    public static PersonalVerdict? MapVerdictFromScore(decimal? score)
    {
        if (!score.HasValue || score <= 0)
        {
            return null;
        }

        if (score is >= 85m)
        {
            return PersonalVerdict.Loved;
        }

        if (score is >= 70m)
        {
            return PersonalVerdict.Liked;
        }

        if (score is >= 55m)
        {
            return PersonalVerdict.Okay;
        }

        if (score is >= 35m)
        {
            return PersonalVerdict.DidNotLike;
        }

        return PersonalVerdict.AvoidType;
    }

    public static string GetPredictedLabel(decimal score)
    {
        if (score >= 82m) return "Very likely";
        if (score >= 68m) return "Likely";
        if (score >= 52m) return "Maybe";
        if (score >= 36m) return "Unlikely";
        return "Avoid";
    }

    public static string GetPredictedGradeLabel(decimal score)
        => RecommendationViewHelper.GetGradeLabel(score switch
        {
            >= 82m => UserGrade.Loved,
            >= 68m => UserGrade.Liked,
            >= 40m => UserGrade.Meh,
            _ => UserGrade.CouldntFinish
        });

    public static IReadOnlyList<string> SplitCsv(string? csv) => RecommendationViewHelper.SplitCsv(csv).ToList();

    public static string JoinCsv(IEnumerable<string> values) => RecommendationViewHelper.JoinCsv(values);

    public static IReadOnlyList<string> BuildGenrePairs(IEnumerable<string> genres)
    {
        var ordered = genres
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(NormalizeFeature)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var pairs = new List<string>();
        for (var i = 0; i < ordered.Count; i++)
        {
            for (var j = i + 1; j < ordered.Count; j++)
            {
                pairs.Add($"{ordered[i]} + {ordered[j]}");
            }
        }

        return pairs;
    }

    public static string? GetDecade(int? year)
    {
        if (!year.HasValue)
        {
            return null;
        }

        var decade = (year.Value / 10) * 10;
        return decade.ToString(CultureInfo.InvariantCulture) + "s";
    }

    public static string? GetRuntimeBucket(int? runtimeMinutes)
    {
        if (!runtimeMinutes.HasValue || runtimeMinutes <= 0)
        {
            return null;
        }

        return runtimeMinutes.Value switch
        {
            < 80 => "Short",
            <= 105 => "Standard",
            <= 135 => "Long",
            _ => "Epic"
        };
    }

    public static IReadOnlyList<string> GetReasonTagHints(IEnumerable<string> reasonTags, string? plot, IEnumerable<string> plotKeywords)
    {
        var loweredSource = ((plot ?? string.Empty) + " " + string.Join(' ', plotKeywords)).ToLowerInvariant();

        return reasonTags
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim())
            .Where(tag =>
            {
                if (!ReasonTagKeywordHints.TryGetValue(tag, out var hints))
                {
                    return false;
                }

                return hints.Any(hint => loweredSource.Contains(hint, StringComparison.OrdinalIgnoreCase));
            })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static IReadOnlyList<string> InferReasonTagHints(string? plot, IEnumerable<string> plotKeywords)
    {
        var loweredSource = ((plot ?? string.Empty) + " " + string.Join(' ', plotKeywords)).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(loweredSource))
        {
            return [];
        }

        return ReasonTagKeywordHints
            .Where(pair => pair.Value.Any(hint => loweredSource.Contains(hint, StringComparison.OrdinalIgnoreCase)))
            .Select(pair => pair.Key)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static IReadOnlyDictionary<string, string[]> ReasonTagHintMap => ReasonTagKeywordHints;
    public static ISet<string> KeywordStopWords => StopWords;

    public static string NormalizeFeature(string value)
        => Regex.Replace(value.Trim().ToLowerInvariant(), @"\s+", " ");
}
