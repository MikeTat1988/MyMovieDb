using LocalMovieVault.Web.Models;

namespace LocalMovieVault.Web.Contracts;

public class SeedMovieRecord
{
    public string Title { get; set; } = string.Empty;
    public string? OriginalTitle { get; set; }
    public int? Year { get; set; }
    public string? Category { get; set; }
    public string? GenresCsv { get; set; }
    public string? Notes { get; set; }
    public decimal? ImdbRating { get; set; }
    public int? RuntimeMinutes { get; set; }
    public string MediaType { get; set; } = nameof(Models.MediaType.Movie);
    public string WatchedStatus { get; set; } = nameof(Models.WatchedStatus.Unknown);
    public decimal? UserRating { get; set; }
    public string? TagsCsv { get; set; }

    public Movie ToEntity()
    {
        Enum.TryParse<MediaType>(MediaType, true, out var mediaType);
        Enum.TryParse<WatchedStatus>(WatchedStatus, true, out var watchedStatus);

        return new Movie
        {
            Title = Title,
            OriginalTitle = OriginalTitle,
            Year = Year,
            Category = Category,
            GenresCsv = GenresCsv,
            Notes = Notes,
            ImdbRating = ImdbRating,
            RuntimeMinutes = RuntimeMinutes,
            MediaType = mediaType,
            WatchedStatus = watchedStatus,
            UserRating = UserRating,
            UserGrade = Helpers.RecommendationViewHelper.MapScoreToGrade(UserRating),
            PrimaryVerdict = Helpers.RecommendationViewHelper.MapGradeToVerdict(Helpers.RecommendationViewHelper.MapScoreToGrade(UserRating)),
            TagsCsv = TagsCsv
        };
    }
}
