using LocalMovieVault.Web.Contracts;

namespace LocalMovieVault.Web.Services;

public interface IMovieMetadataService
{
    Task<MetadataLookupResult> LookupByTitleAsync(string title, int? year, CancellationToken cancellationToken = default);
    Task<MetadataLookupResult> LookupByImdbIdAsync(string imdbId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MetadataSearchCandidate>> SearchCandidatesAsync(string title, int? year, int take = 5, CancellationToken cancellationToken = default);
}
