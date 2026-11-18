using ReviewAgent.Application.Interfaces;
using ReviewAgent.Application.Reviews.Commands.FailReview;
using ReviewAgent.Application.Reviews.Commands.FinishReview;
using ReviewAgent.Application.Reviews.Commands.FinishVerification;
using ReviewAgent.Application.Reviews.Commands.ProposeComment;
using ReviewAgent.Application.Reviews.Commands.RecordToolCall;
using ReviewAgent.Application.Reviews.Commands.RejectComment;

namespace ReviewAgent.Infrastructure.Agents;

/// <summary>
/// The commands the agent saves its steps with, grouped so the runner does not take each one separately.
/// </summary>
internal sealed record ReviewAgentCommands(
    ICommandHandler<RecordToolCallCommand> RecordToolCall,
    ICommandHandler<ProposeCommentCommand, Guid> ProposeComment,
    ICommandHandler<RejectCommentCommand> RejectComment,
    ICommandHandler<FinishVerificationCommand> FinishVerification,
    ICommandHandler<FinishReviewCommand> FinishReview,
    ICommandHandler<FailReviewCommand> FailReview);
