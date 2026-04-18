using LocalMovieVault.Web.Models;

namespace LocalMovieVault.Web.ViewModels;

public class HomeDashboardViewModel
{
    public int TotalCount { get; set; }
    public int WatchedCount { get; set; }
    public int UnwatchedCount { get; set; }
    public decimal? AverageUserRating { get; set; }
    public string? SelectedGenre { get; set; }
    public string DefaultGenre { get; set; } = "Horror";
    public List<string> Genres { get; set; } = [];
    public List<Movie> TopMatches { get; set; } = [];
    public List<Movie> RandomHighlights { get; set; } = [];
}
