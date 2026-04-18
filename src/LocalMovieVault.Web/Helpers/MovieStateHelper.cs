using LocalMovieVault.Web.Models;

namespace LocalMovieVault.Web.Helpers;

public static class MovieStateHelper
{
    public static bool NeedsReview(Movie movie, decimal dismissThreshold)
    {
        if (movie.IsDismissed)
        {
            return false;
        }

        if (movie.WatchedStatus == WatchedStatus.Watched)
        {
            var tagCount = RecommendationViewHelper.SplitCsv(movie.NormalizedTagsCsv ?? movie.ReasonTagsCsv).Count();
            return !movie.UserGrade.HasValue || movie.NeedsTagReview || tagCount < RecommendationViewHelper.GetMinimumReasonTags(movie.UserGrade);
        }

        if (movie.NeedsTagReview)
        {
            return true;
        }

        var score = RecommendationViewHelper.GetDisplayMatchScore(movie);
        return score < dismissThreshold;
    }

    public static string? GetReviewBadge(Movie movie, decimal dismissThreshold)
    {
        if (movie.IsDismissed)
        {
            return "Dismissed";
        }

        if (movie.WatchedStatus == WatchedStatus.Watched &&
            (!movie.UserGrade.HasValue || movie.NeedsTagReview || RecommendationViewHelper.SplitCsv(movie.NormalizedTagsCsv ?? movie.ReasonTagsCsv).Count() < RecommendationViewHelper.GetMinimumReasonTags(movie.UserGrade)))
        {
            return "Needs tags";
        }

        if (movie.NeedsTagReview)
        {
            return "Review later";
        }

        var score = RecommendationViewHelper.GetDisplayMatchScore(movie);
        return score < dismissThreshold ? "Suggested dismiss" : null;
    }

    public static bool IsRecommendationCandidate(Movie movie, decimal dismissThreshold)
        => movie.WatchedStatus != WatchedStatus.Watched
           && !movie.IsDismissed
           && !NeedsReview(movie, dismissThreshold);
}
