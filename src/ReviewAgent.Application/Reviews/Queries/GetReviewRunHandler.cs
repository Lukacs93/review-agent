using ReviewAgent.Application.Interfaces;
using ReviewAgent.Domain.Reviews;

namespace ReviewAgent.Application.Reviews.Queries;

/// <summary>Returns a review run as plain data for the UI, or <see langword="null"/> if it does not exist.</summary>
public sealed class GetReviewRunHandler(IReviewRunRepository repository)
    : IQueryHandler<GetReviewRun, ReviewRunDetails?>
{
    public async Task<ReviewRunDetails?> HandleAsync(GetReviewRun query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var run = await repository.FindAsync(new ReviewRunId(query.RunId), cancellationToken);
        return run is null ? null : ReviewRunDetails.From(run);
    }
}
