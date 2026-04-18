using LocalMovieVault.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace LocalMovieVault.Web.Services;

public class MetadataBackfillService
{
    private readonly AppDbContext _dbContext;
    private readonly IMovieMetadataService _metadataService;

    public MetadataBackfillService(AppDbContext dbContext, IMovieMetadataService metadataService)
    {
        _dbContext = dbContext;
        _metadataService = metadataService;
    }

    public async Task<(bool Success, string Message, int UpdatedCount)> EnrichMissingAsync(int maxItems, CancellationToken cancellationToken = default)
    {
        var candidates = await _dbContext.Movies
            .Where(x => string.IsNullOrWhiteSpace(x.PosterUrl) ||
                        !x.ImdbRating.HasValue ||
                        string.IsNullOrWhiteSpace(x.Overview) ||
                        string.IsNullOrWhiteSpace(x.ExternalId))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        candidates = candidates
            .OrderByDescending(x => !string.IsNullOrWhiteSpace(x.Title) && x.Title.All(c => c <= 127))
            .ThenByDescending(x => x.Year.HasValue)
            .ThenByDescending(x => x.ImdbRating ?? 0)
            .ThenBy(x => string.IsNullOrWhiteSpace(x.OriginalTitle) ? 1 : 0)
            .ThenBy(x => x.UpdatedUtc)
            .Take(Math.Clamp(maxItems, 1, 80))
            .ToList();

        if (candidates.Count == 0)
        {
            return (true, "Все карточки уже заполнены достаточно хорошо.", 0);
        }

        var updated = 0;
        var lookedUp = 0;
        var notFoundTitles = new List<string>();

        foreach (var candidate in candidates)
        {
            var movie = await _dbContext.Movies.FirstAsync(x => x.Id == candidate.Id, cancellationToken);
            var result = await _metadataService.LookupByTitleAsync(movie.Title, movie.Year, cancellationToken);
            lookedUp++;

            if (!result.Success)
            {
                notFoundTitles.Add(movie.Title);
                continue;
            }

            var changed = false;
            changed |= FillIfMissing(movie.OriginalTitle, result.OriginalTitle, x => movie.OriginalTitle = x);
            changed |= FillIfMissing(movie.Category, result.Category, x => movie.Category = x);
            changed |= FillIfMissing(movie.GenresCsv, result.GenresCsv, x => movie.GenresCsv = x);
            changed |= FillIfMissing(movie.Country, result.Country, x => movie.Country = x);
            changed |= FillIfMissing(movie.Language, result.Language, x => movie.Language = x);
            changed |= FillIfMissing(movie.PosterUrl, result.PosterUrl, x => movie.PosterUrl = x);
            changed |= FillIfMissing(movie.Overview, result.Overview, x => movie.Overview = x);
            changed |= FillIfMissing(movie.ExternalId, result.ExternalId, x => movie.ExternalId = x);
            changed |= FillIfMissing(movie.ExternalSource, result.ExternalSource, x => movie.ExternalSource = x);

            if (!movie.ImdbRating.HasValue && result.ImdbRating.HasValue)
            {
                movie.ImdbRating = result.ImdbRating;
                changed = true;
            }

            if (!movie.RuntimeMinutes.HasValue && result.RuntimeMinutes.HasValue)
            {
                movie.RuntimeMinutes = result.RuntimeMinutes;
                changed = true;
            }

            if (!movie.Year.HasValue && result.Year.HasValue)
            {
                movie.Year = result.Year;
                changed = true;
            }

            if (changed)
            {
                updated++;
            }
        }

        if (updated > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        if (lookedUp == 0)
        {
            return (false, "Не нашлось карточек для дозаполнения.", 0);
        }

        if (updated > 0)
        {
            var skippedPart = notFoundTitles.Count > 0
                ? $" Не найдены: {string.Join(", ", notFoundTitles.Take(5))}{(notFoundTitles.Count > 5 ? "…" : string.Empty)}."
                : string.Empty;

            return (true, $"Дозаполнено карточек: {updated} из {lookedUp}.{skippedPart}", updated);
        }

        if (notFoundTitles.Count > 0)
        {
            return (false, $"OMDb не нашёл первые карточки: {string.Join(", ", notFoundTitles.Take(5))}{(notFoundTitles.Count > 5 ? "…" : string.Empty)}.", 0);
        }

        return (true, "Новых данных не найдено.", 0);
    }

    private static bool FillIfMissing(string? existing, string? incoming, Action<string> setter)
    {
        if (!string.IsNullOrWhiteSpace(existing) || string.IsNullOrWhiteSpace(incoming))
        {
            return false;
        }

        setter(incoming.Trim());
        return true;
    }
}
