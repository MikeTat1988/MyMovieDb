using System.Text.RegularExpressions;
using LocalMovieVault.Web.Contracts;
using LocalMovieVault.Web.Models;

namespace LocalMovieVault.Web.Services;

public static class MovieRecordCleaner
{
    private static readonly Dictionary<string, SeedMovieRecord> ManualFixes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["The Legend of Gal"] = new SeedMovieRecord
        {
            Title = "The Legend of Galgameth",
            Category = "Misc",
            GenresCsv = "Misc",
            Notes = "With Tom",
            ImdbRating = 6.0m,
            RuntimeMinutes = 110,
            MediaType = nameof(Models.MediaType.Movie),
            WatchedStatus = nameof(Models.WatchedStatus.Unknown)
        },
        ["The Devil’s Doubl"] = new SeedMovieRecord
        {
            Title = "The Devil’s Double",
            Category = "Drama",
            GenresCsv = "Drama",
            ImdbRating = 7.0m,
            RuntimeMinutes = 109
        },
        ["The Cursed (Eight"] = new SeedMovieRecord
        {
            Title = "The Cursed (Eight for Silver)",
            Category = "Fantasy",
            GenresCsv = "Fantasy",
            ImdbRating = 6.2m,
            RuntimeMinutes = 111
        },
        ["Man Behind the Su"] = new SeedMovieRecord
        {
            Title = "Man Behind the Sun",
            Category = "Drama",
            GenresCsv = "Drama",
            ImdbRating = 6.1m,
            RuntimeMinutes = 105
        },
        ["Jaula (The Chalk"] = new SeedMovieRecord
        {
            Title = "Jaula (The Chalk Line)",
            Category = "Thriller",
            GenresCsv = "Thriller",
            ImdbRating = 6.1m,
            RuntimeMinutes = 106
        },
        ["The Dark and the"] = new SeedMovieRecord
        {
            Title = "The Dark and the Wicked",
            Category = "Horror",
            GenresCsv = "Horror",
            Notes = "Very liked",
            ImdbRating = 6.1m,
            RuntimeMinutes = 95
        },
        ["Судная ночь в Арк"] = new SeedMovieRecord
        {
            Title = "Судная ночь в Аркадии",
            Category = "Horror",
            GenresCsv = "Horror",
            ImdbRating = 5.5m,
            RuntimeMinutes = 92
        },
        ["You Should Have L"] = new SeedMovieRecord
        {
            Title = "You Should Have Left",
            Category = "Horror",
            GenresCsv = "Horror",
            ImdbRating = 5.4m,
            RuntimeMinutes = 93
        },
        ["Tu Tambien lo Har"] = new SeedMovieRecord
        {
            Title = "Tu Tambien lo Haras",
            Category = "Thriller",
            GenresCsv = "Thriller"
        },
        ["Once Upon a Time"] = new SeedMovieRecord
        {
            Title = "Once Upon a Time in Ireland",
            Category = "Crime",
            GenresCsv = "Crime",
            RuntimeMinutes = 75
        }
    };

    public static SeedMovieRecord Clean(string title, string? category, string? notes, string? imdbRatingText, string? runtimeText)
    {
        if (ManualFixes.TryGetValue(title.Trim(), out var fixedRecord))
        {
            var fixedCopy = Clone(fixedRecord);
            ApplyDerivedFields(fixedCopy);
            return fixedCopy;
        }

        var cleanedTitle = title.Trim();
        var (finalTitle, year) = ExtractYear(cleanedTitle);

        var record = new SeedMovieRecord
        {
            Title = finalTitle,
            Year = year,
            Category = CleanupToken(category),
            GenresCsv = CleanupToken(category),
            Notes = CleanupToken(notes),
            ImdbRating = ParseDecimal(imdbRatingText),
            RuntimeMinutes = ParseInt(runtimeText)
        };

        ApplyDerivedFields(record);
        return record;
    }

    public static void ApplyDerivedFields(SeedMovieRecord record)
    {
        if (string.IsNullOrWhiteSpace(record.Title))
        {
            record.Title = "Untitled";
        }

        record.MediaType = InferMediaType(record.Title, record.Category);
        var watched = InferWatchedStatus(record.Notes);
        record.WatchedStatus = watched.Status.ToString();
        record.UserRating = watched.UserRating;
    }

    private static string InferMediaType(string title, string? category)
    {
        if (string.Equals(category, "Series", StringComparison.OrdinalIgnoreCase) ||
            title.Contains("(series)", StringComparison.OrdinalIgnoreCase) ||
            title.Contains("mini", StringComparison.OrdinalIgnoreCase))
        {
            return MediaType.Series.ToString();
        }

        return MediaType.Movie.ToString();
    }

    private static (WatchedStatus Status, decimal? UserRating) InferWatchedStatus(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes))
        {
            return (WatchedStatus.Unknown, null);
        }

        var normalized = notes.Trim().ToLowerInvariant();

        if (normalized.Contains("liked first two"))
        {
            return (WatchedStatus.Unknown, null);
        }

        if (normalized.Contains("favorite"))
        {
            return (WatchedStatus.Watched, 10.0m);
        }

        if (normalized.Contains("loved cinematography"))
        {
            return (WatchedStatus.Watched, 8.8m);
        }

        if (normalized.Contains("loved"))
        {
            return (WatchedStatus.Watched, 9.5m);
        }

        if (normalized.Contains("very liked"))
        {
            return (WatchedStatus.Watched, 8.5m);
        }

        if (normalized.Contains("liked very much"))
        {
            return (WatchedStatus.Watched, 8.8m);
        }

        if (normalized.Contains("liked"))
        {
            return (WatchedStatus.Watched, 8.0m);
        }

        if (normalized.Contains("nice"))
        {
            return (WatchedStatus.Watched, 7.2m);
        }

        if (normalized.Contains("meh"))
        {
            return (WatchedStatus.Watched, 5.0m);
        }

        if (normalized.Contains("rewatch"))
        {
            return (WatchedStatus.Watched, 8.0m);
        }

        return (WatchedStatus.Unknown, null);
    }

    private static (string Title, int? Year) ExtractYear(string title)
    {
        var match = Regex.Match(title, @"^(?<title>.+?)\s+\(?(?<year>(?:19|20)\d{2})\)?$");
        if (!match.Success)
        {
            return (title, null);
        }

        var baseTitle = match.Groups["title"].Value.Trim();
        if (!Regex.IsMatch(baseTitle, @"[\p{L}]"))
        {
            return (title, null);
        }

        return (baseTitle, int.Parse(match.Groups["year"].Value));
    }

    private static decimal? ParseDecimal(string? value)
    {
        value = CleanupToken(value);
        if (string.IsNullOrWhiteSpace(value) || value.Contains('/'))
        {
            return null;
        }

        value = value.Replace(",", ".", StringComparison.Ordinal);

        var match = Regex.Match(value, @"(\d+(?:\.\d+)?)");
        return match.Success ? decimal.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) : null;
    }

    private static int? ParseInt(string? value)
    {
        value = CleanupToken(value);
        if (string.IsNullOrWhiteSpace(value) || value.Contains('/'))
        {
            return null;
        }

        var match = Regex.Match(value, @"(\d+)");
        return match.Success ? int.Parse(match.Groups[1].Value) : null;
    }

    private static string? CleanupToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var cleaned = value.Replace('\u00A0', ' ').Trim();
        return cleaned is "—" or "–" or "-" or "--" or "- -" ? null : cleaned;
    }

    private static SeedMovieRecord Clone(SeedMovieRecord source)
    {
        return new SeedMovieRecord
        {
            Title = source.Title,
            OriginalTitle = source.OriginalTitle,
            Year = source.Year,
            Category = source.Category,
            GenresCsv = source.GenresCsv,
            Notes = source.Notes,
            ImdbRating = source.ImdbRating,
            RuntimeMinutes = source.RuntimeMinutes,
            MediaType = source.MediaType,
            WatchedStatus = source.WatchedStatus,
            UserRating = source.UserRating,
            TagsCsv = source.TagsCsv
        };
    }
}
