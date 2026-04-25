using System.ComponentModel.DataAnnotations;
using LocalMovieVault.Web.Helpers;
using LocalMovieVault.Web.Models;

namespace LocalMovieVault.Web.ViewModels;

public class MovieEditViewModel
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "Title is required.")]
    [Display(Name = "Title")]
    public string Title { get; set; } = string.Empty;

    [Display(Name = "Original title")]
    public string? OriginalTitle { get; set; }

    [Display(Name = "Year")]
    [Range(1888, 2100)]
    public int? Year { get; set; }

    [Display(Name = "Primary category")]
    public string? Category { get; set; }

    [Display(Name = "Genres")]
    public string? GenresCsv { get; set; }

    [Display(Name = "Type")]
    public MediaType MediaType { get; set; } = MediaType.Movie;

    [Display(Name = "IMDb rating")]
    [Range(0, 10)]
    public decimal? ImdbRating { get; set; }

    [Display(Name = "IMDb votes")]
    public int? ImdbVotes { get; set; }

    [Display(Name = "Metascore")]
    public int? Metascore { get; set; }

    [Display(Name = "Runtime (min)")]
    public int? RuntimeMinutes { get; set; }

    [Display(Name = "Released")]
    [DataType(DataType.Date)]
    public DateTime? ReleasedOn { get; set; }

    [Display(Name = "Country")]
    public string? Country { get; set; }

    [Display(Name = "Language")]
    public string? Language { get; set; }

    [Display(Name = "Director")]
    public string? Director { get; set; }

    [Display(Name = "Writer")]
    public string? Writer { get; set; }

    [Display(Name = "Actors")]
    public string? Actors { get; set; }

    [Display(Name = "Poster URL")]
    public string? PosterUrl { get; set; }

    [Display(Name = "Plot")]
    public string? Overview { get; set; }

    [Display(Name = "My notes")]
    public string? Notes { get; set; }

    [Display(Name = "My rating")]
    [Range(0, 100)]
    public decimal? UserRating { get; set; }

    [Display(Name = "Grade")]
    public UserGrade? UserGrade { get; set; }

    [Display(Name = "Quick verdict")]
    public PersonalVerdict? PrimaryVerdict { get; set; }

    [Display(Name = "Normalized tags")]
    public string? ReasonTagsCsv { get; set; }

    public List<string> SelectedReasonTags { get; set; } = [];

    public bool NeedsTagReview { get; set; }

    [Display(Name = "My tags")]
    public string? TagsCsv { get; set; }

    [Display(Name = "Watch status")]
    public WatchedStatus WatchedStatus { get; set; } = WatchedStatus.Unknown;

    public string? ExternalId { get; set; }
    public string? ExternalSource { get; set; }
    public string? OmdbType { get; set; }
    public string? OmdbRatingsJson { get; set; }
    public string? TmdbId { get; set; }
    public string? TmdbKeywordsCsv { get; set; }
    public string? SimilarTitlesJson { get; set; }
    public string? ExternalRatingsJson { get; set; }
}

public static class MovieEditViewModelMapper
{
    public static MovieEditViewModel FromEntity(Movie movie)
    {
        return new MovieEditViewModel
        {
            Id = movie.Id,
            Title = movie.Title,
            OriginalTitle = movie.OriginalTitle,
            Year = movie.Year,
            Category = movie.Category,
            GenresCsv = movie.GenresCsv,
            MediaType = movie.MediaType,
            ImdbRating = movie.ImdbRating,
            ImdbVotes = movie.ImdbVotes,
            Metascore = movie.Metascore,
            RuntimeMinutes = movie.RuntimeMinutes,
            ReleasedOn = movie.ReleasedOn,
            Country = movie.Country,
            Language = movie.Language,
            Director = movie.Director,
            Writer = movie.Writer,
            Actors = movie.Actors,
            PosterUrl = movie.PosterUrl,
            Overview = movie.Overview,
            Notes = movie.Notes,
            UserRating = movie.UserRating,
            UserGrade = movie.UserGrade,
            PrimaryVerdict = movie.PrimaryVerdict,
            ReasonTagsCsv = movie.NormalizedTagsCsv ?? movie.ReasonTagsCsv,
            SelectedReasonTags = RecommendationViewHelper.SplitCsv(movie.NormalizedTagsCsv ?? movie.ReasonTagsCsv).ToList(),
            NeedsTagReview = movie.NeedsTagReview,
            TagsCsv = movie.TagsCsv,
            WatchedStatus = movie.WatchedStatus,
            ExternalId = movie.ExternalId,
            ExternalSource = movie.ExternalSource,
            OmdbType = movie.OmdbType,
            OmdbRatingsJson = movie.OmdbRatingsJson,
            TmdbId = movie.TmdbId,
            TmdbKeywordsCsv = movie.TmdbKeywordsCsv,
            SimilarTitlesJson = movie.SimilarTitlesJson,
            ExternalRatingsJson = movie.ExternalRatingsJson
        };
    }

    public static Movie ToEntity(this MovieEditViewModel model)
    {
        return new Movie
        {
            Id = model.Id ?? 0,
            Title = model.Title,
            OriginalTitle = model.OriginalTitle,
            Year = model.Year,
            Category = model.Category,
            GenresCsv = model.GenresCsv,
            MediaType = model.MediaType,
            ImdbRating = model.ImdbRating,
            ImdbVotes = model.ImdbVotes,
            Metascore = model.Metascore,
            RuntimeMinutes = model.RuntimeMinutes,
            ReleasedOn = model.ReleasedOn,
            Country = model.Country,
            Language = model.Language,
            Director = model.Director,
            Writer = model.Writer,
            Actors = model.Actors,
            PosterUrl = model.PosterUrl,
            Overview = model.Overview,
            Notes = model.Notes,
            UserRating = model.UserRating,
            UserGrade = model.UserGrade,
            PrimaryVerdict = model.PrimaryVerdict,
            ReasonTagsCsv = RecommendationViewHelper.JoinCsv((model.SelectedReasonTags?.Count > 0 ? model.SelectedReasonTags : RecommendationViewHelper.SplitCsv(model.ReasonTagsCsv)).Take(RecommendationViewHelper.MaxReasonTags)),
            NormalizedTagsCsv = RecommendationViewHelper.JoinCsv((model.SelectedReasonTags?.Count > 0 ? model.SelectedReasonTags : RecommendationViewHelper.SplitCsv(model.ReasonTagsCsv)).Take(RecommendationViewHelper.MaxReasonTags)),
            NeedsTagReview = model.NeedsTagReview,
            TagsCsv = model.TagsCsv,
            WatchedStatus = model.WatchedStatus,
            ExternalId = model.ExternalId,
            ExternalSource = model.ExternalSource,
            OmdbType = model.OmdbType,
            OmdbRatingsJson = model.OmdbRatingsJson,
            TmdbId = model.TmdbId,
            TmdbKeywordsCsv = model.TmdbKeywordsCsv,
            SimilarTitlesJson = model.SimilarTitlesJson,
            ExternalRatingsJson = model.ExternalRatingsJson
        };
    }
}
