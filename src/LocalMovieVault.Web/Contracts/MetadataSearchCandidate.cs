using LocalMovieVault.Web.Models;

namespace LocalMovieVault.Web.Contracts;

public class MetadataSearchCandidate
{
    public string ImdbId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? OriginalTitle { get; set; }
    public int? Year { get; set; }
    public MediaType MediaType { get; set; } = MediaType.Movie;
    public decimal? ImdbRating { get; set; }
    public int? ImdbVotes { get; set; }
    public int? RuntimeMinutes { get; set; }
    public string? PosterUrl { get; set; }
    public string? GenresCsv { get; set; }
}
