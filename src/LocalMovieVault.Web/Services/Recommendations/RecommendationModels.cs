using LocalMovieVault.Web.Models;

namespace LocalMovieVault.Web.Services.Recommendations;

public sealed record RecommendationFeatureSet(
    IReadOnlyList<string> Genres,
    IReadOnlyList<string> GenrePairs,
    IReadOnlyList<string> Directors,
    IReadOnlyList<string> Writers,
    IReadOnlyList<string> Actors,
    IReadOnlyList<string> Countries,
    IReadOnlyList<string> Languages,
    IReadOnlyList<string> PlotKeywords,
    IReadOnlyList<string> ReasonTagHints,
    string? Decade,
    string? RuntimeBucket,
    decimal QualityConfidence,
    decimal MetadataQualityScore);

public sealed record ExplanationFactor(string Label, decimal Weight, bool IsPositive);

public sealed class SimilarMovieSummary
{
    public string Title { get; set; } = string.Empty;
    public PersonalVerdict Verdict { get; set; }
    public decimal SimilarityScore { get; set; }
}

public sealed class RecommendationContext
{
    public string Title { get; set; } = string.Empty;
    public decimal FinalScore { get; set; }
    public string PredictedLabel { get; set; } = "Maybe";
    public List<string> PlotKeywords { get; set; } = [];
    public List<ExplanationFactor> PositiveFactors { get; set; } = [];
    public List<ExplanationFactor> NegativeFactors { get; set; } = [];
    public List<SimilarMovieSummary> SimilarToLiked { get; set; } = [];
    public List<SimilarMovieSummary> SimilarToDisliked { get; set; } = [];
    public List<string> MatchedKeywords { get; set; } = [];
    public List<string> WarningFactors { get; set; } = [];
}

public sealed class RecommendationResult
{
    public decimal FinalScore { get; set; }
    public string PredictedLabel { get; set; } = "Maybe";
    public string PredictedReason { get; set; } = string.Empty;
    public decimal PersonalMatchScore { get; set; }
    public RecommendationContext Context { get; set; } = new();
}

public sealed class UserTasteProfile
{
    public Dictionary<string, decimal> GenreWeights { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, decimal> GenrePairWeights { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, decimal> DirectorWeights { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, decimal> WriterWeights { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, decimal> ActorWeights { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, decimal> CountryWeights { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, decimal> LanguageWeights { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, decimal> DecadeWeights { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, decimal> RuntimeWeights { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, decimal> PlotKeywordWeights { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, decimal> ReasonTagWeights { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<Movie> RatedMovies { get; } = [];
}
