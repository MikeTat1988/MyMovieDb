using LocalMovieVault.Web.Models;

namespace LocalMovieVault.Web.Helpers;

public static class GenreHelper
{
    public static IReadOnlyList<string> SplitGenres(string? genresCsv, string? fallbackCategory = null)
    {
        var values = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        AddTokens(values, genresCsv);
        AddTokens(values, fallbackCategory);

        return values.ToList();
    }

    public static string? GetPrimaryGenre(Movie movie)
    {
        return GetPrimaryGenre(movie.GenresCsv, movie.Category);
    }

    public static string? GetPrimaryGenre(string? genresCsv, string? fallbackCategory = null)
    {
        return SplitGenres(genresCsv, fallbackCategory).FirstOrDefault();
    }

    public static bool MatchesGenre(Movie movie, string? genre)
    {
        if (string.IsNullOrWhiteSpace(genre))
        {
            return true;
        }

        return SplitGenres(movie.GenresCsv, movie.Category)
            .Any(x => string.Equals(x, genre, StringComparison.OrdinalIgnoreCase));
    }

    public static List<string> CollectGenres(IEnumerable<Movie> movies)
    {
        return movies
            .SelectMany(x => SplitGenres(x.GenresCsv, x.Category))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToList();
    }

    private static void AddTokens(HashSet<string> target, string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return;
        }

        foreach (var token in raw.Split([',', '/', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!string.IsNullOrWhiteSpace(token))
            {
                target.Add(token);
            }
        }
    }
}
