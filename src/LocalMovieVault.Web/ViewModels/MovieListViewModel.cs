using LocalMovieVault.Web.Models;

namespace LocalMovieVault.Web.ViewModels;

public class MovieListViewModel
{
    public int TotalCount { get; set; }
    public int NotWatchedCount { get; set; }
    public int WatchedCount { get; set; }
    public int ReviewCount { get; set; }
    public int DismissedCount { get; set; }
    public string Section { get; set; } = "not-watched";
    public string? Query { get; set; }
    public string? Genre { get; set; }
    public string WatchedFilter { get; set; } = "all";
    public string SortBy { get; set; } = "personal";
    public List<string> Genres { get; set; } = [];
    public List<Movie> Movies { get; set; } = [];
    public decimal DismissScoreThreshold { get; set; }
}
