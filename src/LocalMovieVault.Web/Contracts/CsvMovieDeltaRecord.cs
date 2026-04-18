namespace LocalMovieVault.Web.Contracts;

public sealed class CsvMovieDeltaRecord
{
    public string Title { get; set; } = string.Empty;
    public int? Year { get; set; }
    public decimal? LegacyUserScore { get; set; }
    public string? UserGrade { get; set; }
    public string? NewTagsCsv { get; set; }
    public string? Action { get; set; }
    public string? ReviewStatus { get; set; }
    public string? Source { get; set; }
    public string? OriginalTagsCsv { get; set; }
    public string? DroppedLegacyTagsCsv { get; set; }
    public string? Notes { get; set; }
}
