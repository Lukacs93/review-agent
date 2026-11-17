using Microsoft.Extensions.DependencyInjection;
using ReviewAgent.Application.Interfaces;
using ReviewAgent.Application.Reviews.Commands.ApproveComment;
using ReviewAgent.Application.Reviews.Commands.DiscardComment;
using ReviewAgent.Application.Reviews.Commands.EditComment;
using ReviewAgent.Application.Reviews.Commands.FailReview;
using ReviewAgent.Application.Reviews.Commands.FinishReview;
using ReviewAgent.Application.Reviews.Commands.ProposeComment;
using ReviewAgent.Application.Reviews.Commands.RecordToolCall;
using ReviewAgent.Application.Reviews.Commands.RejectComment;
using ReviewAgent.Application.Reviews.Commands.StartReview;
using ReviewAgent.Application.Reviews.Queries.GetReviewRun;
using ReviewAgent.Application.SourceControl;
using ReviewAgent.Application.SourceControl.Queries.GetPullRequest;

namespace ReviewAgent.Application;

/// <summary>Registers the command and query handlers of the Application layer.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton(TimeProvider.System);

        services.AddScoped<ICommandHandler<StartReviewCommand, Guid>, StartReviewCommandHandler>();
        services.AddScoped<ICommandHandler<ApproveCommentCommand>, ApproveCommentCommandHandler>();
        services.AddScoped<ICommandHandler<EditCommentCommand>, EditCommentCommandHandler>();
        services.AddScoped<ICommandHandler<DiscardCommentCommand>, DiscardCommentCommandHandler>();

        // The agent's steps.
        services.AddScoped<ICommandHandler<RecordToolCallCommand>, RecordToolCallCommandHandler>();
        services.AddScoped<ICommandHandler<ProposeCommentCommand, Guid>, ProposeCommentCommandHandler>();
        services.AddScoped<ICommandHandler<RejectCommentCommand>, RejectCommentCommandHandler>();
        services.AddScoped<ICommandHandler<FinishReviewCommand>, FinishReviewCommandHandler>();
        services.AddScoped<ICommandHandler<FailReviewCommand>, FailReviewCommandHandler>();

        services.AddScoped<IQueryHandler<GetReviewRunQuery, ReviewRunDetails?>, GetReviewRunQueryHandler>();
        services.AddScoped<IQueryHandler<GetPullRequestQuery, PullRequestSnapshot>, GetPullRequestQueryHandler>();

        return services;
    }
}
