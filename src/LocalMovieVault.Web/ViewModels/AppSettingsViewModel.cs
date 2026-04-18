using LocalMovieVault.Web.Models;
using LocalMovieVault.Web.Services;

namespace LocalMovieVault.Web.ViewModels;

public sealed class AppSettingsViewModel
{
    public required AppUserPreferences Preferences { get; init; }
    public List<string> Genres { get; init; } = [];
    public List<string> ImportantTagOptions { get; init; } = [];
    public List<string> TastePrioritySelections { get; init; } = ["", "", "", ""];
    public Movie? PreviewMovie { get; init; }
    public decimal PreviewCurrentPredictedScore { get; init; }
    public decimal PreviewPredictedScore { get; init; }
    public string PreviewStatusText { get; init; } = string.Empty;
}
