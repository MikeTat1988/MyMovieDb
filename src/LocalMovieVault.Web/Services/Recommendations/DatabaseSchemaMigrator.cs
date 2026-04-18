using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace LocalMovieVault.Web.Services.Recommendations;

public sealed class DatabaseSchemaMigrator
{
    public async Task MigrateAsync(DbContext dbContext, CancellationToken cancellationToken = default)
    {
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);

        var connection = (SqliteConnection)dbContext.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        var existingColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "PRAGMA table_info('Movies');";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                existingColumns.Add(reader.GetString(1));
            }
        }

        var addColumns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ImdbVotes"] = "INTEGER NULL",
            ["Metascore"] = "INTEGER NULL",
            ["ReleasedOn"] = "TEXT NULL",
            ["Director"] = "TEXT NULL",
            ["Writer"] = "TEXT NULL",
            ["Actors"] = "TEXT NULL",
            ["OmdbType"] = "TEXT NULL",
            ["OmdbRatingsJson"] = "TEXT NULL",
            ["ExternalRatingsJson"] = "TEXT NULL",
            ["PrimaryVerdict"] = "INTEGER NULL",
            ["UserGrade"] = "INTEGER NULL",
            ["ReasonTagsCsv"] = "TEXT NULL",
            ["NormalizedTagsCsv"] = "TEXT NULL",
            ["PlotKeywordsCsv"] = "TEXT NULL",
            ["TmdbKeywordsCsv"] = "TEXT NULL",
            ["SimilarTitlesJson"] = "TEXT NULL",
            ["PredictedScore"] = "REAL NULL",
            ["PredictedLabel"] = "TEXT NULL",
            ["PredictedReason"] = "TEXT NULL",
            ["RecommendationContextJson"] = "TEXT NULL",
            ["IsDismissed"] = "INTEGER NOT NULL DEFAULT 0",
            ["NeedsTagReview"] = "INTEGER NOT NULL DEFAULT 0",
            ["DismissedUtc"] = "TEXT NULL",
            ["DismissedReasonTagsCsv"] = "TEXT NULL",
            ["TmdbId"] = "TEXT NULL"
        };

        foreach (var (column, type) in addColumns)
        {
            if (existingColumns.Contains(column))
            {
                continue;
            }

            await using var command = connection.CreateCommand();
            command.CommandText = $"ALTER TABLE Movies ADD COLUMN {column} {type};";
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        var indexStatements = new[]
        {
            "CREATE INDEX IF NOT EXISTS IX_Movies_PredictedScore ON Movies (PredictedScore);",
            "CREATE INDEX IF NOT EXISTS IX_Movies_UserGrade ON Movies (UserGrade);",
            "CREATE INDEX IF NOT EXISTS IX_Movies_PrimaryVerdict ON Movies (PrimaryVerdict);",
            "CREATE INDEX IF NOT EXISTS IX_Movies_IsDismissed ON Movies (IsDismissed);",
            "CREATE INDEX IF NOT EXISTS IX_Movies_NeedsTagReview ON Movies (NeedsTagReview);"
        };

        foreach (var statement in indexStatements)
        {
            await dbContext.Database.ExecuteSqlRawAsync(statement, cancellationToken);
        }

        const string createAppEventsTable = """
            CREATE TABLE IF NOT EXISTS AppEvents (
                Id INTEGER NOT NULL CONSTRAINT PK_AppEvents PRIMARY KEY AUTOINCREMENT,
                Scope TEXT NOT NULL,
                EventType TEXT NOT NULL,
                Outcome TEXT NOT NULL,
                Summary TEXT NOT NULL,
                DetailsJson TEXT NULL,
                RelatedMovieId INTEGER NULL,
                RelatedMovieTitle TEXT NULL,
                RequestPath TEXT NULL,
                OccurredUtc TEXT NOT NULL
            );
            """;
        await dbContext.Database.ExecuteSqlRawAsync(createAppEventsTable, cancellationToken);

        var eventIndexStatements = new[]
        {
            "CREATE INDEX IF NOT EXISTS IX_AppEvents_OccurredUtc ON AppEvents (OccurredUtc DESC);",
            "CREATE INDEX IF NOT EXISTS IX_AppEvents_EventType ON AppEvents (EventType);",
            "CREATE INDEX IF NOT EXISTS IX_AppEvents_Scope ON AppEvents (Scope);"
        };

        foreach (var statement in eventIndexStatements)
        {
            await dbContext.Database.ExecuteSqlRawAsync(statement, cancellationToken);
        }
    }
}
