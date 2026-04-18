using LocalMovieVault.Web.Services.Recommendations;

namespace LocalMovieVault.Web.Services;

public class PersonalMatchService
{
    private readonly IRecommendationEngine _recommendationEngine;

    public PersonalMatchService(IRecommendationEngine recommendationEngine)
    {
        _recommendationEngine = recommendationEngine;
    }

    public Task RecalculateAsync(CancellationToken cancellationToken = default)
        => _recommendationEngine.RecalculateAsync(cancellationToken);
}
