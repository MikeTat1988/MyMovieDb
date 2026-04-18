using System.Globalization;
using System.Text;
using LocalMovieVault.Web.Contracts;

namespace LocalMovieVault.Web.Services;

public sealed class CsvMovieDeltaImportService
{
    public async Task<List<CsvMovieDeltaRecord>> ParseAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var csv = await reader.ReadToEndAsync(cancellationToken);
        var rows = ParseCsv(csv);
        if (rows.Count <= 1)
        {
            return [];
        }

        var header = rows[0];
        var index = header
            .Select((name, i) => new { name = name.Trim(), i })
            .ToDictionary(x => x.name, x => x.i, StringComparer.OrdinalIgnoreCase);

        return rows
            .Skip(1)
            .Where(x => x.Any(cell => !string.IsNullOrWhiteSpace(cell)))
            .Select(row => new CsvMovieDeltaRecord
            {
                Title = Get(row, index, "Title"),
                Year = TryParseInt(Get(row, index, "Year")),
                LegacyUserScore = TryParseDecimal(Get(row, index, "LegacyUserScore")),
                UserGrade = Get(row, index, "UserGrade"),
                NewTagsCsv = Get(row, index, "NewTagsCsv"),
                Action = Get(row, index, "Action"),
                ReviewStatus = Get(row, index, "ReviewStatus"),
                Source = Get(row, index, "Source"),
                OriginalTagsCsv = Get(row, index, "OriginalTagsCsv"),
                DroppedLegacyTagsCsv = Get(row, index, "DroppedLegacyTagsCsv"),
                Notes = Get(row, index, "Notes")
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.Title))
            .GroupBy(x => $"{TitleNormalizer.Normalize(x.Title)}|{x.Year}", StringComparer.OrdinalIgnoreCase)
            .Select(x => x.Last())
            .ToList();
    }

    private static string Get(IReadOnlyList<string> row, IReadOnlyDictionary<string, int> index, string column)
        => index.TryGetValue(column, out var i) && i < row.Count ? row[i].Trim() : string.Empty;

    private static int? TryParseInt(string value)
        => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;

    private static decimal? TryParseDecimal(string value)
        => decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;

    private static List<List<string>> ParseCsv(string csv)
    {
        var rows = new List<List<string>>();
        var currentRow = new List<string>();
        var currentCell = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < csv.Length; i++)
        {
            var ch = csv[i];
            if (inQuotes)
            {
                if (ch == '"')
                {
                    if (i + 1 < csv.Length && csv[i + 1] == '"')
                    {
                        currentCell.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    currentCell.Append(ch);
                }

                continue;
            }

            switch (ch)
            {
                case '"':
                    inQuotes = true;
                    break;
                case ',':
                    currentRow.Add(currentCell.ToString());
                    currentCell.Clear();
                    break;
                case '\r':
                    break;
                case '\n':
                    currentRow.Add(currentCell.ToString());
                    currentCell.Clear();
                    rows.Add(currentRow);
                    currentRow = [];
                    break;
                default:
                    currentCell.Append(ch);
                    break;
            }
        }

        if (currentCell.Length > 0 || currentRow.Count > 0)
        {
            currentRow.Add(currentCell.ToString());
            rows.Add(currentRow);
        }

        return rows;
    }
}
