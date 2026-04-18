using LocalMovieVault.Web.Models;

namespace LocalMovieVault.Web.ViewModels;

public sealed class DiagnosticsViewModel
{
    public int EventCount => Events.Count;

    public IReadOnlyList<AppEvent> Events { get; init; } = [];
}
