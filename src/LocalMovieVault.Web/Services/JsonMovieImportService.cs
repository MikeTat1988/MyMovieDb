using System.Text.Json;
using LocalMovieVault.Web.Contracts;

namespace LocalMovieVault.Web.Services;

public class JsonMovieImportService
{
    public async Task<List<SeedMovieRecord>> ParseAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        var records = await JsonSerializer.DeserializeAsync<List<SeedMovieRecord>>(stream, cancellationToken: cancellationToken)
                      ?? [];

        foreach (var record in records)
        {
            MovieRecordCleaner.ApplyDerivedFields(record);
        }

        return records
            .Where(x => !string.IsNullOrWhiteSpace(x.Title))
            .GroupBy(x => $"{TitleNormalizer.Normalize(x.Title)}|{x.Year}|{x.MediaType}", StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .ToList();
    }
}
