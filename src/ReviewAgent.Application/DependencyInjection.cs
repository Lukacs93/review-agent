using Microsoft.Extensions.DependencyInjection;
using ReviewAgent.Application.Interfaces;
using ReviewAgent.Application.Reviews.Commands;
using ReviewAgent.Application.Reviews.Queries;
using ReviewAgent.Application.SourceControl;
using ReviewAgent.Application.SourceControl.Queries;

namespace ReviewAgent.Application;

/// <summary>Registers the command and query handlers of the Application layer.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton(TimeProvider.System);

        services.AddScoped<ICommandHandler<StartReview, Guid>, StartReviewHandler>();
        services.AddScoped<ICommandHandler<ApproveComment>, ApproveCommentHandler>();
        services.AddScoped<ICommandHandler<EditComment>, EditCommentHandler>();
        services.AddScoped<ICommandHandler<DiscardComment>, DiscardCommentHandler>();

        // The agent's steps.
        services.AddScoped<ICommandHandler<RecordToolCall>, RecordToolCallHandler>();
        services.AddScoped<ICommandHandler<ProposeComment, Guid>, ProposeCommentHandler>();
        services.AddScoped<ICommandHandler<RejectComment>, RejectCommentHandler>();
        services.AddScoped<ICommandHandler<FinishReview>, FinishReviewHandler>();
        services.AddScoped<ICommandHandler<FailReview>, FailReviewHandler>();

        services.AddScoped<IQueryHandler<GetReviewRun, ReviewRunDetails?>, GetReviewRunHandler>();
        services.AddScoped<IQueryHandler<GetPullRequest, PullRequestSnapshot>, GetPullRequestHandler>();

        return services;
    }
}
