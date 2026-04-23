using System.Text.Json;
using LocalMovieVault.Web.Helpers;
using LocalMovieVault.Web.Models;
using LocalMovieVault.Web.Services.Recommendations;

namespace LocalMovieVault.Web.ViewModels;

public sealed class MovieDetailsViewModel
{
    private static readonly JsonSerializerOptions RecommendationContextJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly HashSet<string> ToneLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        "Tense",
        "Scary",
        "Disturbing atmosphere",
        "Emotional",
        "Funny",
        "Charming",
        "Immersive",
        "Thought-provoking",
        "Comfort watch",
        "Weird in a good way"
    };

    private static readonly HashSet<string> StyleLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        "Incredible visuals",
        "Great dialogue",
        "Great acting",
        "Exciting action",
        "Epic scale",
        "Great worldbuilding",
        "Attention to detail",
        "Believable characters",
        "Strong ending",
        "Original idea"
    };

    private static readonly HashSet<string> StoryStopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "and", "for", "with", "this", "that", "from", "into", "your", "their", "have", "after", "before",
        "about", "through", "story", "movie", "film", "plot", "when", "they", "them", "then", "than", "hero",
        "heroes", "back", "take", "takes", "make", "made", "over", "under", "where", "while", "whose", "what",
        "are", "art"
    };

    public required Movie Movie { get; init; }
    public required decimal DismissScoreThreshold { get; init; }
    public RecommendationContext? RecommendationContext { get; init; }
    public Movie? ReferenceMovie { get; init; }
    public List<ComparisonRow> ComparisonRows { get; init; } = [];
    public string ReviewBadge => MovieStateHelper.GetReviewBadge(Movie, DismissScoreThreshold) ?? "Ready";
    public decimal DisplayScore => RecommendationViewHelper.GetDisplayMatchScore(Movie);
    public string FitLabel => GetFitLabel(DisplayScore);
    public bool ShowHeroReviewAction => Movie.WatchedStatus != WatchedStatus.Watched || !RecommendationViewHelper.HasCompletedReview(Movie);
    public string HeroSlotTitle => ShowHeroReviewAction
        ? "Review"
        : RecommendationViewHelper.GetGradeLabel(Movie.UserGrade);
    public string HeroSlotSubtitle => ShowHeroReviewAction
        ? Movie.WatchedStatus == WatchedStatus.Watched ? "Finish your review" : "Add your watch verdict"
        : "Your rating";
    public bool ShowComparison => ReferenceMovie is not null && ComparisonRows.Count > 0;
    public bool HasShortSummary => !string.IsNullOrWhiteSpace(Movie.Overview);
    public IReadOnlyList<KeyDetailRow> KeyDetails => BuildKeyDetails(Movie);

    public static MovieDetailsViewModel Create(Movie movie, decimal dismissScoreThreshold, Movie? referenceMovie = null)
    {
        RecommendationContext? context = null;
        if (!string.IsNullOrWhiteSpace(movie.RecommendationContextJson))
        {
            try
            {
                context = JsonSerializer.Deserialize<RecommendationContext>(movie.RecommendationContextJson, RecommendationContextJsonOptions);
            }
            catch
            {
                context = null;
            }
        }

        return new MovieDetailsViewModel
        {
            Movie = movie,
            DismissScoreThreshold = dismissScoreThreshold,
            RecommendationContext = context,
            ReferenceMovie = referenceMovie,
            ComparisonRows = BuildComparisonRows(movie, referenceMovie, context)
        };
    }

    private static List<ComparisonRow> BuildComparisonRows(Movie movie, Movie? referenceMovie, RecommendationContext? context)
    {
        if (referenceMovie is null)
        {
            return [];
        }

        return
        [
            new(
                "Important tags",
                BuildImportantTagsSummary(movie, context),
                BuildImportantTagsSummary(referenceMovie, null),
                BuildImportantTagsNote(context)),
            new(
                "Story / themes",
                BuildStoryThemeSummary(movie, context),
                BuildStoryThemeSummary(referenceMovie, null),
                BuildStoryThemeNote(context)),
            new(
                "Tone / vibe",
                BuildToneSummary(movie, context),
                BuildToneSummary(referenceMovie, null),
                BuildToneNote(context)),
            new(
                "Style / pull",
                BuildStyleSummary(movie, context),
                BuildStyleSummary(referenceMovie, null),
                BuildStyleNote(context)),
            new(
                "Risk factors",
                BuildRiskSummary(context),
                BuildReferenceConfidenceSummary(referenceMovie),
                BuildRiskNote(context))
        ];
    }

    private static IReadOnlyList<KeyDetailRow> BuildKeyDetails(Movie movie)
        =>
        [
            new("Genres", string.IsNullOrWhiteSpace(movie.GenresCsv) ? "-" : movie.GenresCsv),
            new("Runtime", movie.RuntimeMinutes.HasValue ? $"{movie.RuntimeMinutes.Value} min" : "-"),
            new("Status", movie.WatchedStatus == WatchedStatus.Watched
                ? (movie.NeedsTagReview ? "Under review" : "Watched")
                : "Unwatched"),
            new("Your rating", RecommendationViewHelper.HasCompletedReview(movie)
                ? RecommendationViewHelper.GetGradeLabel(movie.UserGrade)
                : "Not finished")
        ];

    private static string GetFitLabel(decimal score)
        => score switch
        {
            >= 82m => "Strong fit",
            >= 68m => "Good fit",
            >= 52m => "Mixed fit",
            >= 36m => "Risky fit",
            _ => "Weak fit"
        };

    private static string BuildImportantTagsSummary(Movie movie, RecommendationContext? context)
    {
        var values = ExtractTagLabels(context, "priority:", "tag:")
            .Concat(RecommendationViewHelper.SplitCsv(movie.NormalizedTagsCsv ?? movie.ReasonTagsCsv))
            .Where(IsUsefulLabel)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3);

        return JoinForDisplay(values, "Limited tag signal");
    }

    private static string BuildStoryThemeSummary(Movie movie, RecommendationContext? context)
    {
        var values = ExtractFactorValues(context, "story:")
            .Concat(RecommendationViewHelper.SplitCsv(movie.PlotKeywordsCsv))
            .Select(CleanStoryValue)
            .Where(IsUsefulStoryValue)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3);

        return JoinForDisplay(values, "Limited story signal");
    }

    private static string BuildToneSummary(Movie movie, RecommendationContext? context)
    {
        var values = ExtractTagLabels(context, "tag:", "priority:")
            .Concat(RecommendationViewHelper.SplitCsv(movie.NormalizedTagsCsv ?? movie.ReasonTagsCsv))
            .Where(x => ToneLabels.Contains(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3);

        return JoinForDisplay(values, "Tone not strongly mapped yet");
    }

    private static string BuildStyleSummary(Movie movie, RecommendationContext? context)
    {
        var values = ExtractTagLabels(context, "tag:", "priority:")
            .Concat(RecommendationViewHelper.SplitCsv(movie.NormalizedTagsCsv ?? movie.ReasonTagsCsv))
            .Where(x => StyleLabels.Contains(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3);

        return JoinForDisplay(values, "Style pull still thin");
    }

    private static string BuildRiskSummary(RecommendationContext? context)
    {
        var values = context?.NegativeFactors
            .OrderByDescending(x => x.Weight)
            .Select(FormatFactorLabel)
            .Where(IsUsefulLabel)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(2)
            .ToList() ?? [];

        if (values.Count == 0)
        {
            return "No major blocker logged";
        }

        return string.Join(", ", values);
    }

    private static string BuildReferenceConfidenceSummary(Movie referenceMovie)
    {
        if (RecommendationViewHelper.HasCompletedReview(referenceMovie))
        {
            return $"{RecommendationViewHelper.GetGradeLabel(referenceMovie.UserGrade)} by you";
        }

        if (referenceMovie.WatchedStatus == WatchedStatus.Watched)
        {
            return "Watched reference";
        }

        return "Thin anchor";
    }

    private static string BuildImportantTagsNote(RecommendationContext? context)
    {
        var priorities = ExtractFactorValues(context, "priority:").Take(2).ToList();
        return priorities.Count == 0
            ? "Shows the strongest taste tags the app used here."
            : $"Weighted by your priorities like {string.Join(", ", priorities)}.";
    }

    private static string BuildStoryThemeNote(RecommendationContext? context)
    {
        var storySignals = ExtractFactorValues(context, "story:").Take(2).ToList();
        return storySignals.Count == 0
            ? "Uses the clearest story signals the app could extract."
            : $"The app pulled story signals like {string.Join(", ", storySignals)}.";
    }

    private static string BuildToneNote(RecommendationContext? context)
    {
        var blockers = context?.NegativeFactors.Any(x => FormatFactorLabel(x).Contains("Risk", StringComparison.OrdinalIgnoreCase)) == true;
        return blockers
            ? "Tone is one place where the fit looks unstable."
            : "Useful when genre alone is too broad to explain the recommendation.";
    }

    private static string BuildStyleNote(RecommendationContext? context)
        => context?.PositiveFactors.Any(x => x.Label.StartsWith("tag:", StringComparison.OrdinalIgnoreCase)) == true
            ? "This is where visuals, action, acting, and worldbuilding show up."
            : "Shows the craft/style pull the app could identify.";

    private static string BuildRiskNote(RecommendationContext? context)
        => context?.WarningFactors.Count > 0
            ? string.Join(" ", context.WarningFactors.Select(x => $"{x}."))
            : "Summarizes the biggest reason the app is still uncertain.";

    private static IEnumerable<string> ExtractTagLabels(RecommendationContext? context, params string[] prefixes)
        => context is null
            ? []
            : context.PositiveFactors
                .Concat(context.NegativeFactors)
                .Select(x => x.Label)
                .Where(label => prefixes.Any(prefix => label.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
                .Select(label => FormatFactorLabel(label))
                .Where(IsUsefulLabel);

    private static IEnumerable<string> ExtractFactorValues(RecommendationContext? context, params string[] prefixes)
        => context is null
            ? []
            : context.PositiveFactors
                .Concat(context.NegativeFactors)
                .Select(x => x.Label)
                .Where(label => prefixes.Any(prefix => label.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
                .Select(label => FormatFactorLabel(label))
                .Where(IsUsefulLabel);

    private static string FormatFactorLabel(ExplanationFactor factor)
        => FormatFactorLabel(factor.Label);

    private static string FormatFactorLabel(string label)
    {
        var cleaned = label;
        foreach (var prefix in new[] { "tag:", "priority:", "story:", "genre:", "genre mix:", "director:", "writer:", "actor:", "language:", "country:", "preference:" })
        {
            if (cleaned.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                cleaned = cleaned[prefix.Length..].Trim();
                break;
            }
        }

        return cleaned.Trim();
    }

    private static string CleanStoryValue(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length == 0)
        {
            return string.Empty;
        }

        return trimmed.Replace("-", " ");
    }

    private static bool IsUsefulStoryValue(string value)
        => value.Length >= 4
           && value.Any(char.IsLetter)
           && !StoryStopWords.Contains(value);

    private static bool IsUsefulLabel(string value)
        => !string.IsNullOrWhiteSpace(value)
           && !string.Equals(value, "-", StringComparison.Ordinal)
           && value.Length >= 3;

    private static string JoinForDisplay(IEnumerable<string> values, string fallback)
    {
        var list = values
            .Select(x => x.Trim())
            .Where(x => x.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToList();

        return list.Count == 0 ? fallback : string.Join(", ", list);
    }
}

public sealed record ComparisonRow(string Parameter, string CurrentValue, string ReferenceValue, string Note);

public sealed record KeyDetailRow(string Label, string Value);
