using System.Text.Json;
using LocalMovieVault.Web.Helpers;
using LocalMovieVault.Web.Models;
using LocalMovieVault.Web.Services.Recommendations;

namespace LocalMovieVault.Web.ViewModels;

public sealed class MovieDetailsViewModel
{
    public required Movie Movie { get; init; }
    public required decimal DismissScoreThreshold { get; init; }
    public RecommendationContext? RecommendationContext { get; init; }
    public List<DecisionTableRow> DecisionRows { get; init; } = [];
    public string ReviewBadge => MovieStateHelper.GetReviewBadge(Movie, DismissScoreThreshold) ?? "Ready";

    public static MovieDetailsViewModel Create(Movie movie, decimal dismissScoreThreshold)
    {
        RecommendationContext? context = null;
        if (!string.IsNullOrWhiteSpace(movie.RecommendationContextJson))
        {
            try
            {
                context = JsonSerializer.Deserialize<RecommendationContext>(movie.RecommendationContextJson);
            }
            catch
            {
                context = null;
            }
        }

        var rows = new List<DecisionTableRow>
        {
            new("IMDb", movie.ImdbRating?.ToString("0.0") ?? "-", BuildLikedBenchmark(context, "well-supported public ratings")),
            new("Genre", FirstGenre(movie), BuildLikedBenchmark(context, "genre")),
            new("App taste", RecommendationViewHelper.GetDisplayMatchScore(movie).ToString("0.#"), BuildLikedBenchmark(context, "similar to movies you liked")),
            new("Story", MatchSummary(context, "story"), BuildLikedBenchmark(context, "story")),
            new("Cinematography", MatchSummary(context, "cinematography"), BuildLikedBenchmark(context, "cinematography"))
        };

        return new MovieDetailsViewModel
        {
            Movie = movie,
            DismissScoreThreshold = dismissScoreThreshold,
            RecommendationContext = context,
            DecisionRows = rows
        };
    }

    private static string FirstGenre(Movie movie)
        => RecommendationViewHelper.SplitCsv(movie.GenresCsv).FirstOrDefault() ?? "-";

    private static string MatchSummary(RecommendationContext? context, string label)
    {
        if (context is null)
        {
            return "Limited data";
        }

        var factor = context.PositiveFactors
            .Concat(context.NegativeFactors)
            .FirstOrDefault(x => x.Label.Contains(label, StringComparison.OrdinalIgnoreCase));

        return factor is null ? "Limited data" : $"{(factor.IsPositive ? "Supports" : "Weakens")} fit";
    }

    private static string BuildLikedBenchmark(RecommendationContext? context, string label)
    {
        if (context?.SimilarToLiked.Count > 0)
        {
            var weakestLiked = context.SimilarToLiked.OrderBy(x => x.SimilarityScore).First();
            return $"{weakestLiked.Title} ({weakestLiked.SimilarityScore:0.#})";
        }

        var factor = context?.PositiveFactors.FirstOrDefault(x => x.Label.Contains(label, StringComparison.OrdinalIgnoreCase));
        return factor is null ? "-" : factor.Label;
    }
}

public sealed record DecisionTableRow(string Parameter, string CurrentGrade, string Benchmark);
