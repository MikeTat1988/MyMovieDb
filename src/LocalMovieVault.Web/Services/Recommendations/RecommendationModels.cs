using LocalMovieVault.Web.Models;

namespace LocalMovieVault.Web.Services.Recommendations;

public sealed record RecommendationFeatureSet(
    IReadOnlyList<string> Genres,
    IReadOnlyList<string> GenrePairs,
    IReadOnlyList<string> HybridSignals,
    IReadOnlyList<string> ToneSignals,
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

public sealed record ExternalQualitySignal(
    string Key,
    decimal Strength,
    string Source,
    bool IsRisk);

public sealed record ReviewDerivedSignals(
    IReadOnlyList<string> CraftRisks,
    IReadOnlyList<string> TasteDescriptors,
    IReadOnlyList<string> SpecialCases,
    decimal QualityRiskScore,
    decimal Confidence,
    IReadOnlyList<ExternalQualitySignal>? QualitySignals = null);

public sealed record ExplanationFactor(string Label, decimal Weight, bool IsPositive);

public sealed class SimilarMovieSummary
{
    public string Title { get; set; } = string.Empty;
    public PersonalVerdict Verdict { get; set; }
    public decimal SimilarityScore { get; set; }
    public bool IsWowPick { get; set; }
}

public sealed class RecommendationContext
{
    public string Title { get; set; } = string.Empty;
    public decimal FinalScore { get; set; }
    public decimal AffinityScore { get; set; }
    public decimal ConfidenceScore { get; set; }
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
    public Dictionary<string, decimal> HybridSignalWeights { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, decimal> ToneSignalWeights { get; } = new(StringComparer.OrdinalIgnoreCase);
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

public sealed record RecommendationWeightProfile
{
    public decimal BroadFitScale { get; init; } = 2.85m;
    public decimal SimilarityScale { get; init; } = 31.8m;
    public decimal AffinityBaseOffset { get; init; } = 24m;
    public decimal PositiveTagScoreScale { get; init; } = 2.6m;
    public decimal NegativeTagScoreScale { get; init; } = 2.8m;
    public decimal PositiveTagContributionScale { get; init; } = 0.42m;
    public decimal NegativeTagContributionScale { get; init; } = 0.56m;
    public decimal ConfidenceBaseOffset { get; init; } = 18m;
    public decimal MetadataConfidenceScale { get; init; } = 0.34m;
    public decimal QualityConfidenceScale { get; init; } = 0.22m;
    public decimal StrongPositiveConfidenceScale { get; init; } = 4.5m;
    public decimal StrongNegativeConfidencePenaltyScale { get; init; } = 1.5m;
    public decimal SimilarityEvidenceCap { get; init; } = 22m;
    public decimal FinalScoreScale { get; init; } = 0.86m;
    public decimal RankBonusScale { get; init; } = 2.0m;
    public decimal ConfidenceCenter { get; init; } = 58m;
    public decimal ConfidenceAdjustmentScale { get; init; } = 0.07m;
    public decimal FinalScoreCap { get; init; } = 94m;
    public decimal BroadMatchConfidencePenalty { get; init; } = 18m;
    public decimal QuietAtmosphericConfidencePenalty { get; init; } = 7m;
    public decimal WeakQuietAnchorConfidencePenalty { get; init; } = 12m;
    public decimal InferredCalibrationPenalty { get; init; } = 3.0m;
    public decimal QuietAtmosphericCalibrationPenalty { get; init; } = 2.5m;
    public decimal HorrorDramaCalibrationPenalty { get; init; } = 1.2m;
    public decimal WeakQuietAnchorCalibrationPenalty { get; init; } = 3.4m;
    public decimal IndiaCalibrationPenalty { get; init; } = 1.35m;
    public decimal TopBandGuardPenalty { get; init; } = 1.4m;
    public decimal WeakQuietAnchorSimilarityThreshold { get; init; } = 26m;
    public decimal ComedyHybridMismatchPenalty { get; init; } = 0.05m;
    public decimal HumorousSeriousMismatchPenalty { get; init; } = 0.06m;
    public decimal PlayfulDreadMismatchPenalty { get; init; } = 0.05m;
    public decimal SpectacleAtmosphericMismatchPenalty { get; init; } = 0.04m;
    public decimal KineticReflectiveMismatchPenalty { get; init; } = 0.03m;
    public decimal QuietIntenseMismatchPenalty { get; init; } = 0.15m;

    public static RecommendationWeightProfile CreateDefault() => new();
}
