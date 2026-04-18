using System.Globalization;
using LocalMovieVault.Web.Data;
using LocalMovieVault.Web.Helpers;
using LocalMovieVault.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace LocalMovieVault.Web.Services;

public sealed class ExportCsvRestoreService
{
    public sealed class ExportRow
    {
        public string Title { get; set; } = string.Empty;
        public int? Year { get; set; }
        public string Watched { get; set; } = string.Empty;
        public string Dismissed { get; set; } = string.Empty;
        public decimal? UserScore { get; set; }
        public string DismissedReasons { get; set; } = string.Empty;
        public string ReasonTags { get; set; } = string.Empty;
    }

    public async Task<List<ExportRow>> ParseAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        var parser = new CsvMovieDeltaImportService();
        var rows = await parser.ParseAsync(stream, cancellationToken);
        return rows.Select(_ => new ExportRow()).ToList();
    }

    public async Task<List<ExportRow>> ParseExportAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(stream, leaveOpen: true);
        var csv = await reader.ReadToEndAsync(cancellationToken);
        var rawRows = ParseCsv(csv);
        if (rawRows.Count <= 1)
        {
            return [];
        }

        var header = rawRows[0];
        var index = header
            .Select((name, i) => new { name = name.Trim(), i })
            .ToDictionary(x => x.name, x => x.i, StringComparer.OrdinalIgnoreCase);

        return rawRows.Skip(1)
            .Where(x => x.Any(cell => !string.IsNullOrWhiteSpace(cell)))
            .Select(row => new ExportRow
            {
                Title = Get(row, index, "Title"),
                Year = TryParseInt(Get(row, index, "Year")),
                Watched = Get(row, index, "Watched"),
                Dismissed = Get(row, index, "Dismissed"),
                UserScore = TryParseDecimal(Get(row, index, "UserScore")),
                DismissedReasons = Get(row, index, "DismissedReasons"),
                ReasonTags = Get(row, index, "ReasonTags")
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.Title))
            .ToList();
    }

    public async Task ApplyAsync(AppDbContext dbContext, ExportRow row, CancellationToken cancellationToken = default)
    {
        var normalizedTitle = TitleNormalizer.Normalize(row.Title);
        var movie = await dbContext.Movies
            .Where(x => x.NormalizedTitle == normalizedTitle && (!row.Year.HasValue || x.Year == row.Year))
            .OrderByDescending(x => x.Year == row.Year)
            .FirstOrDefaultAsync(cancellationToken);

        if (movie is null)
        {
            return;
        }

        movie.IsDismissed = string.Equals(row.Dismissed, "Yes", StringComparison.OrdinalIgnoreCase);
        movie.DismissedReasonTagsCsv = string.IsNullOrWhiteSpace(row.DismissedReasons) ? null : row.DismissedReasons;
        movie.UserRating = row.UserScore;
        movie.UserGrade = RecommendationViewHelper.MapScoreToGrade(row.UserScore);
        movie.PrimaryVerdict = RecommendationViewHelper.MapGradeToVerdict(movie.UserGrade);
        movie.NormalizedTagsCsv = RecommendationViewHelper.JoinCsv(RecommendationViewHelper.MigrateLegacyTags(row.ReasonTags));
        movie.ReasonTagsCsv = movie.NormalizedTagsCsv;
        movie.NeedsTagReview = movie.UserGrade.HasValue && RecommendationViewHelper.SplitCsv(movie.NormalizedTagsCsv).Count() == 0;
        movie.WatchedStatus = ParseWatchedStatus(row.Watched);
        if (movie.WatchedStatus != WatchedStatus.Watched)
        {
            movie.UserRating = null;
            movie.UserGrade = null;
            movie.PrimaryVerdict = null;
            movie.NormalizedTagsCsv = null;
            movie.ReasonTagsCsv = null;
            movie.NeedsTagReview = false;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static WatchedStatus ParseWatchedStatus(string value)
    {
        if (Enum.TryParse<WatchedStatus>(value, true, out var parsed))
        {
            return parsed;
        }

        return WatchedStatus.Unknown;
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
        var currentCell = new System.Text.StringBuilder();
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
