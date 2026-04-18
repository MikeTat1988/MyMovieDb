using LocalMovieVault.Web.Models;

namespace LocalMovieVault.Web.Services.Recommendations;

public interface IPlotKeywordExtractor
{
    IReadOnlyList<string> ExtractKeywords(Movie movie);
}

public interface IRecommendationFeatureExtractor
{
    RecommendationFeatureSet Extract(Movie movie);
}

public interface IRecommendationExplainer
{
    string BuildReason(RecommendationContext context);
}

public interface IRecommendationEngine
{
    Task RecalculateAsync(CancellationToken cancellationToken = default);
}
