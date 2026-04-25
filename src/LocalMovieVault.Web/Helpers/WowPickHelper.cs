using LocalMovieVault.Web.Models;
using LocalMovieVault.Web.Services;

namespace LocalMovieVault.Web.Helpers;

public static class WowPickHelper
{
    public static int GetWowLimit(int totalMovieCount, AppUserPreferences preferences)
    {
        var tuning = preferences.TasteTuning;
        var computed = (int)Math.Round(totalMovieCount * tuning.WowLimitRatio, MidpointRounding.AwayFromZero);
        return Math.Clamp(computed, tuning.WowMinimumPicks, tuning.WowMaximumPicks);
    }

    public static bool CanAssignWow(Movie movie)
        => movie.WatchedStatus == WatchedStatus.Watched && RecommendationViewHelper.HasCompletedReview(movie);
}
