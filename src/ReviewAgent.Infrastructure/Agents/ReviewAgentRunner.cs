using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReviewAgent.Application.Reviews;
using ReviewAgent.Application.Reviews.Commands;
using ReviewAgent.Application.SourceControl;
using ReviewAgent.Domain.Reviews;
using ReviewAgent.Infrastructure.Agents.Errors;
using ReviewAgent.Infrastructure.Agents.LanguageModel;
using ReviewAgent.Infrastructure.Agents.Tools;
using ReviewAgent.Infrastructure.Rules;

namespace ReviewAgent.Infrastructure.Agents;

/// <summary>
/// Runs the review agent on one review run with Microsoft Agent Framework. The model reads the pull request through
/// the MCP tools and proposes comments through <see cref="ProposeCommentTool"/>; everything it does is saved
/// through the commands. Its limits: <see cref="AgentOptions.MaxToolRounds"/> and <see cref="AgentOptions.Timeout"/>.
/// </summary>
internal sealed partial class ReviewAgentRunner(
    IReviewRunRepository repository,
    IPullRequestSource pullRequestSource,
    IPullRequestToolsConnector toolsConnector,
    IAgentChatClientFactory chatClientFactory,
    ReviewAgentCommands commands,
    IOptions<AgentOptions> options,
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
            var summary = await RunAgentAsync(runId, run.PullRequest, timeoutSource.Token);
            if (!string.IsNullOrWhiteSpace(summary))
            {
                await commands.FinishReview.HandleAsync(new FinishReview(runId, summary), cancellationToken);
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
            failure = AgentErrors.Failed(ModelErrorDetails.Describe(exception));
        }

        await commands.FailReview.HandleAsync(new FailReview(runId, failure), cancellationToken);
    }

    private async Task<string> RunAgentAsync(
        Guid runId,
        PullRequestRef pullRequest,
        CancellationToken cancellationToken)
    {
        var settings = options.Value;

        // Read here too, so propose_comment can check the model's comments against the pull request and the rules.
        var snapshot = await pullRequestSource.GetPullRequestAsync(pullRequest, cancellationToken);
        var rulesMarkdown = await pullRequestSource.GetFileAsync(pullRequest, settings.TeamRulesPath, cancellationToken)
            ?? throw new InvalidOperationException(AgentErrors.TeamRulesNotFound(settings.TeamRulesPath));
        var rules = MarkdownRuleSetParser.Parse(rulesMarkdown);

        await using var connection = await toolsConnector.ConnectAsync(pullRequest, cancellationToken);
        var proposeComment = new ProposeCommentTool(runId, snapshot, rules, commands.ProposeComment);

        List<AITool> tools =
        [
            .. connection.Tools
                .Append(proposeComment.AsAIFunction())
                .Select(tool => new RecordedToolFunction(tool, runId, commands.RecordToolCall)),
        ];

        // The function-invoking client runs the tools the model asks for and sends back the results, until the
        // model answers without asking for more; the agent reuses it instead of adding its own.
        using var chatClient = chatClientFactory.Create()
            .AsBuilder()
            .UseFunctionInvocation(
                loggerFactory,
                functionInvoker => functionInvoker.MaximumIterationsPerRequest = settings.MaxToolRounds)
            .Build();

        var agent = chatClient.AsAIAgent(
            instructions: ReviewInstructions.SystemPrompt,
            name: ReviewInstructions.AgentName,
            tools: tools);

        var response = await agent.RunAsync(ReviewInstructions.TaskPrompt, cancellationToken: cancellationToken);
        return response.Text;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The review of run {RunId} failed.")]
    private static partial void LogReviewFailed(ILogger logger, Guid runId, Exception exception);
}
