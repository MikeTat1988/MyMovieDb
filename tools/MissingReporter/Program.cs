using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;

var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
var dataHome = Path.Combine(docs, "MyMovieDB");
var dbPath = Path.Combine(dataHome, "localmovievault.db");
var outDir = Path.Combine(dataHome, "NotFoundReport");

Console.OutputEncoding = Encoding.UTF8;
Console.WriteLine("=== missing-report ===");
Console.WriteLine($"DB  : {dbPath}");

if (!File.Exists(dbPath))
{
    Console.WriteLine("ERROR: База не найдена.");
    Environment.ExitCode = 1;
    return;
}

Directory.CreateDirectory(outDir);

var rows = new List<Row>();
await using var connection = new SqliteConnection($"Data Source={dbPath}");
await connection.OpenAsync();

var command = connection.CreateCommand();
command.CommandText = @"
SELECT Id, Title, OriginalTitle, Year, Category, GenresCsv, ImdbRating, ExternalId, PosterUrl
FROM Movies
WHERE IFNULL(TRIM(PosterUrl), '') = ''
   OR IFNULL(TRIM(ExternalId), '') = ''
ORDER BY Title;";

await using var reader = await command.ExecuteReaderAsync();
while (await reader.ReadAsync())
{
    var row = new Row(
        reader.GetInt32(0),
        GetString(reader, 1),
        GetString(reader, 2),
        GetNullableInt(reader, 3),
        GetString(reader, 4),
        GetString(reader, 5),
        GetString(reader, 6),
        GetString(reader, 7),
        GetString(reader, 8));

    rows.Add(row with
    {
        Missing = string.Join(", ", new[]
        {
            string.IsNullOrWhiteSpace(row.PosterUrl) ? "Poster" : null,
            string.IsNullOrWhiteSpace(row.ExternalId) ? "ExternalId" : null
        }.Where(x => x is not null)),
        SearchHint = row.Year.HasValue ? $"{row.Title} {row.Year.Value}" : row.Title
    });
}

var stamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
var txtPath = Path.Combine(outDir, $"missing_{stamp}.txt");
var csvPath = Path.Combine(outDir, $"missing_{stamp}.csv");
var jsonPath = Path.Combine(outDir, $"missing_{stamp}.json");

var sb = new StringBuilder();
sb.AppendLine("Missing movies report");
sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
sb.AppendLine($"Count: {rows.Count}");
sb.AppendLine();

foreach (var row in rows)
{
    sb.AppendLine($"[{row.Id}] {row.Title}");
    if (!string.IsNullOrWhiteSpace(row.OriginalTitle)) sb.AppendLine($"  Original: {row.OriginalTitle}");
    if (row.Year.HasValue) sb.AppendLine($"  Year: {row.Year.Value}");
    if (!string.IsNullOrWhiteSpace(row.Genres)) sb.AppendLine($"  Genres: {row.Genres}");
    if (!string.IsNullOrWhiteSpace(row.Category)) sb.AppendLine($"  Category: {row.Category}");
    if (!string.IsNullOrWhiteSpace(row.ImdbRating)) sb.AppendLine($"  IMDb: {row.ImdbRating}");
    sb.AppendLine($"  Missing: {row.Missing}");
    sb.AppendLine($"  SearchHint: {row.SearchHint}");
    sb.AppendLine();
}

await File.WriteAllTextAsync(txtPath, sb.ToString(), Encoding.UTF8);
await File.WriteAllTextAsync(jsonPath, JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }), Encoding.UTF8);
await File.WriteAllTextAsync(csvPath, ToCsv(rows), Encoding.UTF8);

Console.WriteLine($"TXT : {txtPath}");
Console.WriteLine($"CSV : {csvPath}");
Console.WriteLine($"JSON: {jsonPath}");
Console.WriteLine($"Count: {rows.Count}");

static string ToCsv(IEnumerable<Row> rows)
{
    static string Escape(string? value)
    {
        value ??= string.Empty;
        return $"\"{value.Replace("\"", "\"\"")}\"";
    }

    var lines = new List<string>
    {
        "Id,Title,OriginalTitle,Year,Category,Genres,ImdbRating,Missing,SearchHint"
    };

    lines.AddRange(rows.Select(r => string.Join(",", new[]
    {
        r.Id.ToString(),
        Escape(r.Title),
        Escape(r.OriginalTitle),
        Escape(r.Year?.ToString()),
        Escape(r.Category),
        Escape(r.Genres),
        Escape(r.ImdbRating),
        Escape(r.Missing),
        Escape(r.SearchHint)
    })));

    return string.Join(Environment.NewLine, lines);
}

static string? GetString(SqliteDataReader reader, int ordinal)
    => reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);

static int? GetNullableInt(SqliteDataReader reader, int ordinal)
    => reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);

record Row(
    int Id,
    string? Title,
    string? OriginalTitle,
    int? Year,
    string? Category,
    string? Genres,
    string? ImdbRating,
    string? ExternalId,
    string? PosterUrl)
{
    public string? Missing { get; init; }
    public string? SearchHint { get; init; }
}
