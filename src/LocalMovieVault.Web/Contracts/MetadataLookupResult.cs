using LocalMovieVault.Web.Models;

namespace LocalMovieVault.Web.Contracts;

public class MetadataLookupResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }

    public string Title { get; set; } = string.Empty;
    public string? OriginalTitle { get; set; }
    public int? Year { get; set; }
    public string? Category { get; set; }
    public string? GenresCsv { get; set; }
    public decimal? ImdbRating { get; set; }
    public int? ImdbVotes { get; set; }
    public int? Metascore { get; set; }
    public int? RuntimeMinutes { get; set; }
    public DateTime? ReleasedOn { get; set; }
    public string? Country { get; set; }
    public string? Language { get; set; }
    public string? Director { get; set; }
    public string? Writer { get; set; }
    public string? Actors { get; set; }
    public string? Overview { get; set; }
    public string? PosterUrl { get; set; }
    public string? OmdbType { get; set; }
    public string? OmdbRatingsJson { get; set; }
    public string? TmdbId { get; set; }
    public string? TmdbKeywordsCsv { get; set; }
    public string? SimilarTitlesJson { get; set; }
    public string? ExternalRatingsJson { get; set; }
    public string? ExternalId { get; set; }
    public string ExternalSource { get; set; } = "OMDb";
    public MediaType MediaType { get; set; } = MediaType.Movie;
}
