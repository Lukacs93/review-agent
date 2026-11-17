using ReviewAgent.Application.Interfaces;
using ReviewAgent.Application.Reviews.Commands;

namespace ReviewAgent.Infrastructure.Agents;

/// <summary>
/// The commands the agent saves its steps with, grouped so the runner does not take each one separately.
/// </summary>
internal sealed record ReviewAgentCommands(
    ICommandHandler<RecordToolCall> RecordToolCall,
    ICommandHandler<ProposeComment, Guid> ProposeComment,
    ICommandHandler<FinishReview> FinishReview,
    ICommandHandler<FailReview> FailReview);
