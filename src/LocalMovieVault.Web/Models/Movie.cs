using System.ComponentModel.DataAnnotations;

namespace LocalMovieVault.Web.Models;

public class Movie
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? OriginalTitle { get; set; }

    public int? Year { get; set; }

    [MaxLength(120)]
    public string? Category { get; set; }

    [MaxLength(300)]
    public string? GenresCsv { get; set; }

    public MediaType MediaType { get; set; } = MediaType.Movie;

    [Range(0, 10)]
    public decimal? ImdbRating { get; set; }

    public int? ImdbVotes { get; set; }

    public int? Metascore { get; set; }

    public int? RuntimeMinutes { get; set; }

    public DateTime? ReleasedOn { get; set; }

    [MaxLength(200)]
    public string? Country { get; set; }

    [MaxLength(200)]
    public string? Language { get; set; }

    [MaxLength(300)]
    public string? Director { get; set; }

    [MaxLength(500)]
    public string? Writer { get; set; }

    [MaxLength(500)]
    public string? Actors { get; set; }

    [MaxLength(1000)]
    public string? PosterUrl { get; set; }

    public string? Overview { get; set; }

    [MaxLength(40)]
    public string? OmdbType { get; set; }

    public string? OmdbRatingsJson { get; set; }

    public string? Notes { get; set; }

    [Range(0, 100)]
    public decimal? UserRating { get; set; }

    public UserGrade? UserGrade { get; set; }

    public PersonalVerdict? PrimaryVerdict { get; set; }

    [MaxLength(400)]
    public string? ReasonTagsCsv { get; set; }

    [MaxLength(400)]
    public string? NormalizedTagsCsv { get; set; }

    public bool NeedsTagReview { get; set; }

    [MaxLength(300)]
    public string? TagsCsv { get; set; }

    [MaxLength(500)]
    public string? PlotKeywordsCsv { get; set; }

    [MaxLength(500)]
    public string? TmdbKeywordsCsv { get; set; }

    public string? SimilarTitlesJson { get; set; }

    public string? ExternalRatingsJson { get; set; }

    [MaxLength(30)]
    public string? TmdbId { get; set; }

    public WatchedStatus WatchedStatus { get; set; } = WatchedStatus.Unknown;

    [Range(0, 100)]
    public decimal? PersonalMatchScore { get; set; }

    [Range(0, 100)]
    public decimal? PredictedScore { get; set; }

    [MaxLength(20)]
    public string? PredictedLabel { get; set; }

    [MaxLength(300)]
    public string? PredictedReason { get; set; }

    public string? RecommendationContextJson { get; set; }

    public bool IsDismissed { get; set; }

    public DateTime? DismissedUtc { get; set; }

    [MaxLength(400)]
    public string? DismissedReasonTagsCsv { get; set; }

    [MaxLength(30)]
    public string? ExternalSource { get; set; }

    [MaxLength(50)]
    public string? ExternalId { get; set; }

    [MaxLength(250)]
    public string NormalizedTitle { get; set; } = string.Empty;

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}
