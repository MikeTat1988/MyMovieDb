namespace LocalMovieVault.Web.Services.Recommendations;

public sealed class RecommendationExplainer : IRecommendationExplainer
{
    public string BuildReason(RecommendationContext context)
    {
        var positives = context.PositiveFactors
            .OrderByDescending(x => x.Weight)
            .Select(x => x.Label.Replace("tag: ", string.Empty, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToList();

        var risk = context.NegativeFactors
            .OrderByDescending(x => x.Weight)
            .Select(x => x.Label.Replace("tag: ", string.Empty, StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault();
        var likedMatches = context.SimilarToLiked.Take(2).Select(x => x.Title).ToList();

        var why = positives.Count == 0
            ? "Limited personal evidence so far"
            : $"Likely to work because of {string.Join(", ", positives)}";
        if (likedMatches.Count > 0)
        {
            why += $" and similarities to {string.Join(" / ", likedMatches)}";
        }

        return string.IsNullOrWhiteSpace(risk)
            ? why + "."
            : $"{why}. Risk: {risk.ToLowerInvariant()}.";
    }
}
