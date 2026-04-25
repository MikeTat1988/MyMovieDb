using LocalMovieVault.Web.Contracts;

namespace LocalMovieVault.Web.Services;

public sealed class RankedMovieMetadataService : IMovieMetadataService
{
    private readonly OmdbMovieMetadataService _inner;

    public RankedMovieMetadataService(OmdbMovieMetadataService inner)
    {
        _inner = inner;
    }

    public Task<MetadataLookupResult> LookupByTitleAsync(string title, int? year, CancellationToken cancellationToken = default)
        => _inner.LookupByTitleAsync(title, year, cancellationToken);

    public Task<MetadataLookupResult> LookupByImdbIdAsync(string imdbId, CancellationToken cancellationToken = default)
        => _inner.LookupByImdbIdAsync(imdbId, cancellationToken);

    public async Task<IReadOnlyList<MetadataSearchCandidate>> SearchCandidatesAsync(string title, int? year, int take = 5, CancellationToken cancellationToken = default)
    {
        var requestedTake = Math.Max(take, 1);
        var baseCandidates = await _inner.SearchCandidatesAsync(title, year, requestedTake, cancellationToken);
        if (baseCandidates.Count == 0)
        {
            return baseCandidates;
        }

        var enriched = new List<MetadataSearchCandidate>(baseCandidates.Count);
        foreach (var candidate in baseCandidates)
        {
            var detail = await _inner.LookupByImdbIdAsync(candidate.ImdbId, cancellationToken);
            enriched.Add(new MetadataSearchCandidate
            {
                ImdbId = candidate.ImdbId,
                Title = detail.Success ? detail.Title : candidate.Title,
                OriginalTitle = detail.Success ? detail.OriginalTitle : candidate.OriginalTitle,
                Year = detail.Success ? detail.Year : candidate.Year,
                MediaType = detail.Success ? detail.MediaType : candidate.MediaType,
                ImdbRating = detail.Success ? detail.ImdbRating : candidate.ImdbRating,
                ImdbVotes = detail.Success ? detail.ImdbVotes : candidate.ImdbVotes,
                RuntimeMinutes = detail.Success ? detail.RuntimeMinutes : candidate.RuntimeMinutes,
                PosterUrl = detail.Success ? detail.PosterUrl : candidate.PosterUrl,
                GenresCsv = detail.Success ? detail.GenresCsv : candidate.GenresCsv
            });
        }

        var normalizedTitle = NormalizeForMatch(title);
        return enriched
            .OrderByDescending(x => NormalizeForMatch(x.Title) == normalizedTitle)
            .ThenByDescending(x => year.HasValue && x.Year == year)
            .ThenByDescending(x => (x.ImdbVotes ?? 0))
            .ThenByDescending(x => x.ImdbRating ?? 0m)
            .ThenBy(x => x.Title)
            .Take(requestedTake)
            .ToList();
    }

    private static string NormalizeForMatch(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : new string(value.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
}
