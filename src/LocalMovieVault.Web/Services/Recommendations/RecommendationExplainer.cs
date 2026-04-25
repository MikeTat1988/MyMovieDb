namespace LocalMovieVault.Web.Services.Recommendations;

public sealed class RecommendationExplainer : IRecommendationExplainer
{
    public string BuildReason(RecommendationContext context)
    {
        var positives = context.PositiveFactors
            .OrderByDescending(x => x.Weight)
            .Select(FormatFactor)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToList();

        var blockers = context.NegativeFactors
            .OrderByDescending(x => x.Weight)
            .Select(FormatFactor)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(2)
            .ToList();
        var likedMatches = context.SimilarToLiked
            .Where(x => x.SimilarityScore >= 8m)
            .Take(2)
            .Select(x => x.Title)
            .ToList();
        var evidenceSuffix = BuildEvidenceSuffix(context);

        if (context.FinalScore < 35m)
        {
            var reason = blockers.Count == 0
                ? "Probably not a strong fit based on your current ratings"
                : $"Probably not a strong fit because of {string.Join(", ", blockers)}";
            if (positives.Count > 0)
            {
                reason += $". Small overlap from {string.Join(", ", positives.Take(2))}";
            }

            return reason + evidenceSuffix;
        }

        if (context.FinalScore < 55m)
        {
            var reason = blockers.Count == 0
                ? "Mixed fit with more caution than confidence"
                : $"Mixed fit because of {string.Join(", ", blockers)}";
            if (positives.Count > 0)
            {
                reason += $", despite some support from {string.Join(", ", positives.Take(2))}";
            }

            return reason + evidenceSuffix;
        }

        var why = positives.Count == 0
            ? "Limited personal evidence so far"
            : context.FinalScore >= 78m
                ? $"Likely to work because of {string.Join(", ", positives)}"
                : $"Could work because of {string.Join(", ", positives)}";
        if (likedMatches.Count > 0)
        {
            why += $" and similarities to {string.Join(" / ", likedMatches)}";
        }

        if (blockers.Count > 0 && context.ConfidenceScore >= 55m)
        {
            why += $". Risk: {blockers[0].ToLowerInvariant()}";
        }

        return why + evidenceSuffix;
    }

    private static string FormatFactor(ExplanationFactor factor)
        => FormatFactorLabel(factor.Label);

    private static string FormatFactorLabel(string label)
    {
        foreach (var prefix in new[] { "tag: ", "tone: ", "hybrid: ", "genre mix: ", "genre: ", "story: ", "priority: ", "preference: " })
        {
            if (label.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return label[prefix.Length..].Trim();
            }
        }

        return label.Trim();
    }

    private static string BuildEvidenceSuffix(RecommendationContext context)
    {
        if (context.WarningFactors.Any(x => string.Equals(x, "Broad match", StringComparison.OrdinalIgnoreCase)))
        {
            return ". Broad evidence.";
        }

        if (context.WarningFactors.Any(x => string.Equals(x, "Soft anchor", StringComparison.OrdinalIgnoreCase)))
        {
            return ". Limited evidence.";
        }

        if (context.ConfidenceScore >= 72m)
        {
            return ". Strong evidence.";
        }

        if (context.ConfidenceScore > 0m && context.ConfidenceScore < 45m)
        {
            return ". Limited evidence.";
        }

        return ".";
    }
}
