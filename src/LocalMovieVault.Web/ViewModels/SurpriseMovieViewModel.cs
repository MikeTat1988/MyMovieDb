using LocalMovieVault.Web.Models;

namespace LocalMovieVault.Web.ViewModels;

public sealed class SurpriseMovieViewModel
{
    public Movie? Pick { get; set; }
    public int EligibleCount { get; set; }
    public int TotalCount { get; set; }
}
