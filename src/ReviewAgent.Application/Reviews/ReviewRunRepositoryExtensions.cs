using ReviewAgent.Application.Reviews.Errors;
using ReviewAgent.Domain.Reviews;

namespace ReviewAgent.Application.Reviews;

public static class ReviewRunRepositoryExtensions
{
    /// <summary>Loads a run that must exist; throws <see cref="ReviewRunNotFoundException"/> if it does not.</summary>
    public static async Task<ReviewRun> GetAsync(
        this IReviewRunRepository repository,
        ReviewRunId id,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(repository);

        return await repository.FindAsync(id, cancellationToken)
            ?? throw new ReviewRunNotFoundException(ReviewRunRepositoryErrors.RunNotFound(id));
    }
}
