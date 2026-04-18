using LocalMovieVault.Web.Contracts;

namespace LocalMovieVault.Web.ViewModels;

public class AddMoviePageViewModel
{
    public string? LookupTitle { get; set; }
    public int? LookupYear { get; set; } = 2026;
    public string? LookupMessage { get; set; }
    public bool ShowSavePopup { get; set; }
    public MovieEditViewModel Movie { get; set; } = new();
    public List<MetadataSearchCandidate> Candidates { get; set; } = new();
    public string? SelectedImdbId { get; set; }
}
