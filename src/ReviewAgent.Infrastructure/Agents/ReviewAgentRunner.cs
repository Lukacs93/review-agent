using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReviewAgent.Application.Reviews;
using ReviewAgent.Application.Reviews.Agent;
using ReviewAgent.Application.Reviews.Commands.FailReview;
using ReviewAgent.Application.Reviews.Commands.FinishReview;
using ReviewAgent.Application.Reviews.Commands.FinishVerification;
using ReviewAgent.Application.SourceControl;
using ReviewAgent.Domain.Reviews;
using ReviewAgent.Infrastructure.Agents.Errors;
using ReviewAgent.Infrastructure.Agents.LanguageModel;
using ReviewAgent.Infrastructure.Agents.Tools;
using ReviewAgent.Infrastructure.Rules;

namespace ReviewAgent.Infrastructure.Agents;

/// <summary>
/// Runs the agents on one review run with Microsoft Agent Framework. The reviewer reads the pull request through
/// the MCP tools and proposes comments through <see cref="ProposeCommentTool"/>; then the verifier double-checks
/// them and rejects the wrong ones through <see cref="RejectCommentTool"/>. The two run one after the other, in
/// this order, and everything they do is saved through the commands. Their limits:
/// <see cref="AgentOptions.MaxToolRounds"/> and <see cref="AgentOptions.Timeout"/>.
/// </summary>
internal sealed partial class ReviewAgentRunner(
    IReviewRunRepository repository,
    IPullRequestSource pullRequestSource,
    IPullRequestToolsConnector toolsConnector,
    IAgentChatClientFactory chatClientFactory,
    ReviewAgentCommands commands,
    IOptions<AgentOptions> options,
    IOptions<TeamRulesOptions> teamRules,
    ILoggerFactory loggerFactory) : IReviewAgent
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<ReviewAgentRunner>();

    public async Task ReviewAsync(Guid runId, CancellationToken cancellationToken)
    {
        var run = await repository.GetAsync(new ReviewRunId(runId), cancellationToken);
        if (run.Status != ReviewStatus.Running)
        {
            // Already reviewed, for example because it was queued twice.
            return;
        }

        var timeout = options.Value.Timeout;
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);

        string? failure;
        try
        {
            var (summary, reviewedCommitSha) = await RunAgentAsync(runId, run.PullRequest, timeoutSource.Token);
            if (!string.IsNullOrWhiteSpace(summary))
            {
                await commands.FinishReview.HandleAsync(
                    new FinishReviewCommand(runId, summary, reviewedCommitSha),
                    cancellationToken);
                return;
            }

            failure = AgentErrors.NoSummary;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The application is stopping; the run stays Running.
            throw;
        }
        catch (OperationCanceledException) when (timeoutSource.IsCancellationRequested)
        {
            failure = AgentErrors.TimedOut(timeout);
        }
        catch (Exception exception)
        {
            LogReviewFailed(_logger, runId, exception);
            LogFullModelAnswer(runId, exception);
            failure = AgentErrors.Failed(ModelErrorDetails.Describe(exception));
        }

        await commands.FailReview.HandleAsync(new FailReviewCommand(runId, failure), cancellationToken);
    }

    /// <summary>Runs the agents; returns the summary, and the commit of the pull request they reviewed.</summary>
    private async Task<(string Summary, string? ReviewedCommitSha)> RunAgentAsync(
        Guid runId,
        PullRequestRef pullRequest,
        CancellationToken cancellationToken)
    {
        var settings = options.Value;

        // Read here too, so propose_comment can check the model's comments against the pull request and the rules.
        var snapshot = await pullRequestSource.GetPullRequestAsync(pullRequest, cancellationToken);
        var rulesPath = teamRules.Value.TeamRulesPath;
        var rulesMarkdown = await pullRequestSource.GetFileAsync(pullRequest, rulesPath, cancellationToken)
            ?? throw new InvalidOperationException(AgentErrors.TeamRulesNotFound(rulesPath));
        var rules = MarkdownRuleSetParser.Parse(rulesMarkdown);

        await using var connection = await toolsConnector.ConnectAsync(pullRequest, cancellationToken);

        // The function-invoking client runs the tools the model asks for and sends back the results, until the
        // model answers without asking for more; the agents reuse it instead of adding their own.
        using var chatClient = chatClientFactory.Create()
            .AsBuilder()
            .UseFunctionInvocation(
                loggerFactory,
                functionInvoker => functionInvoker.MaximumIterationsPerRequest = settings.MaxToolRounds)
            .Build();

        var proposeComment = new ProposeCommentTool(runId, snapshot, rules, commands.ProposeComment);
        var reviewer = chatClient.AsAIAgent(
            instructions: ReviewInstructions.SystemPrompt,
            name: ReviewInstructions.ReviewerName,
            tools: RecordedTools(
                runId,
                connection.Tools.Append(proposeComment.AsAIFunction()),
                ReviewInstructions.ReviewerName));

        var response = await reviewer.RunAsync(ReviewInstructions.TaskPrompt, cancellationToken: cancellationToken);
        var summary = response.Text;

        if (!settings.Verifier.Enabled || string.IsNullOrWhiteSpace(summary))
        {
            return (summary, snapshot.HeadCommitSha);
        }

        var verificationFailure = await VerifyCommentsAsync(runId, connection.Tools, chatClient, cancellationToken);
        var verifiedSummary = verificationFailure is null
            ? summary
            : $"{summary}\n\n{AgentErrors.VerificationFailed(verificationFailure)}";
        return (verifiedSummary, snapshot.HeadCommitSha);
    }

    /// <summary>
    /// Runs the verifier agent on the comments the reviewer proposed; it rejects the ones the code does not support,
    /// and its closing sentence is saved.
    /// Returns why the verification failed, or <see langword="null"/>. A failed verification does not fail the
    /// review: the reviewer's comments are kept, and the summary says they were not double-checked.
    /// </summary>
    private async Task<string?> VerifyCommentsAsync(
        Guid runId,
        IReadOnlyList<AIFunction> pullRequestTools,
        IChatClient chatClient,
        CancellationToken cancellationToken)
    {
        var run = await repository.GetAsync(new ReviewRunId(runId), cancellationToken);
        var proposed = run.Comments.Where(comment => !comment.IsRejected).ToList();
        if (proposed.Count == 0)
        {
            return null;
        }

        var rejectComment = new RejectCommentTool(
            runId,
            proposed.Select(comment => comment.Id.Value),
            commands.RejectComment);
        var verifier = chatClient.AsAIAgent(
            instructions: ReviewInstructions.VerifierSystemPrompt,
            name: ReviewInstructions.VerifierName,
            tools: RecordedTools(
                runId,
                pullRequestTools.Append(rejectComment.AsAIFunction()),
                ReviewInstructions.VerifierName));

        string verifierAnswer;
        try
        {
            var response = await verifier.RunAsync(
                ReviewInstructions.VerifierTaskPrompt(proposed),
                cancellationToken: cancellationToken);
            verifierAnswer = response.Text;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogVerificationFailed(_logger, runId, exception);
            LogFullModelAnswer(runId, exception);
            return ModelErrorDetails.Describe(exception);
        }

        // The model's own sentence only goes to the log: how many comments it kept and rejected is counted from
        // the rejections it actually made. Saved outside the try: a failure to save is not a failed verification.
        LogVerificationFinished(_logger, runId, verifierAnswer);
        await commands.FinishVerification.HandleAsync(new FinishVerificationCommand(runId), cancellationToken);

        return null;
    }

    private List<AITool> RecordedTools(Guid runId, IEnumerable<AIFunction> tools, string agentName) =>
    [
        .. tools.Select(tool => new RecordedToolFunction(tool, runId, commands.RecordToolCall, agentName)),
    ];

    // The failure reason only has the API's error message; the whole answer is logged, so the details can be looked
    // up here.
    private void LogFullModelAnswer(Guid runId, Exception exception)
    {
        var answer = ModelErrorDetails.AnswerOf(exception);
        if (answer is not null)
        {
            LogModelAnswer(_logger, runId, answer);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The model's API answered for run {RunId}: {Answer}")]
    private static partial void LogModelAnswer(ILogger logger, Guid runId, string answer);

    [LoggerMessage(Level = LogLevel.Information, Message = "The verifier of run {RunId} answered: {Answer}")]
    private static partial void LogVerificationFinished(ILogger logger, Guid runId, string answer);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The review of run {RunId} failed.")]
    private static partial void LogReviewFailed(ILogger logger, Guid runId, Exception exception);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "The verification of run {RunId} failed; its comments are kept unverified.")]
    private static partial void LogVerificationFailed(ILogger logger, Guid runId, Exception exception);
}
