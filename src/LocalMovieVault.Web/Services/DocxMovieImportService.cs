using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using LocalMovieVault.Web.Contracts;

namespace LocalMovieVault.Web.Services;

public class DocxMovieImportService
{
    public async Task<List<SeedMovieRecord>> ParseAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        await using var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream, cancellationToken);
        memoryStream.Position = 0;

        using var document = WordprocessingDocument.Open(memoryStream, false);
        var table = document.MainDocumentPart?.Document.Body?.Elements<Table>().FirstOrDefault();
        if (table is null)
        {
            return [];
        }

        var rows = table.Elements<TableRow>().ToList();
        if (rows.Count <= 1)
        {
            return [];
        }

        var parsedRecords = new List<SeedMovieRecord>();

        foreach (var row in rows.Skip(1))
        {
            var cells = row.Elements<TableCell>().Select(GetCellText).ToList();
            if (cells.Count < 5)
            {
                continue;
            }

            var title = cells[0];
            if (string.IsNullOrWhiteSpace(title))
            {
                continue;
            }

            var record = MovieRecordCleaner.Clean(
                title,
                category: cells.ElementAtOrDefault(1),
                notes: cells.ElementAtOrDefault(2),
                imdbRatingText: cells.ElementAtOrDefault(3),
                runtimeText: cells.ElementAtOrDefault(4));

            parsedRecords.Add(record);
        }

        return parsedRecords
            .GroupBy(x => $"{TitleNormalizer.Normalize(x.Title)}|{x.Year}|{x.MediaType}", StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .ToList();
    }

    private static string GetCellText(TableCell cell)
    {
        return string.Join(" ", cell.Descendants<Text>().Select(x => x.Text))
            .Replace('\u00A0', ' ')
            .Trim();
    }
}
