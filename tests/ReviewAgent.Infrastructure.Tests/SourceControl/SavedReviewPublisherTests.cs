using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using ReviewAgent.Application.Reviews.Publishing;
using ReviewAgent.Domain.Reviews;
using ReviewAgent.Infrastructure.Logging;
using ReviewAgent.Infrastructure.SourceControl.GitHub;
using ReviewAgent.Infrastructure.SourceControl.Saved;
using ReviewAgent.Infrastructure.Tests.Logging;

namespace ReviewAgent.Infrastructure.Tests.SourceControl;

public sealed class SavedReviewPublisherTests : IDisposable
{
    private static readonly Guid _runId = Guid.Parse("6f1c2a9e-3b4d-4e5f-8a7b-1c2d3e4f5a6b");

    private readonly TemporaryDirectory _root = new();

    private static ReviewPublication Publication => new(
        _runId,
        new PullRequestRef("test", "demo", 1),
        ReviewedCommitSha: null,
        "One architecture issue.",
        [new CommentToPublish("src/Order.cs", 12, "ARCH-01", "Use an application handler.", SuggestedFix: null)]);

    private string PublishedFile =>
        Path.Combine(_root.Path, "test", "demo", "1", $"{_runId}.json");

    private SavedReviewPublisher Publisher => new(_root.Path, NullLogger<SavedReviewPublisher>.Instance);

    [Fact]
    public async Task Review_is_written_as_the_request_GitHub_would_get()
    {
        await Publisher.PublishAsync(Publication, CancellationToken.None);

        using var written = JsonDocument.Parse(await File.ReadAllTextAsync(PublishedFile));
        var review = written.RootElement;
        Assert.Equal(
            $"One architecture issue.\n\n{GitHubReviewRequest.MarkerOf(_runId)}",
            review.GetProperty("body").GetString());
        var comment = Assert.Single(review.GetProperty("comments").EnumerateArray().ToList());
        Assert.Equal("**ARCH-01**: Use an application handler.", comment.GetProperty("body").GetString());
        Assert.Single(Directory.GetFiles(Path.GetDirectoryName(PublishedFile)!));
    }

    [Fact]
    public async Task Review_counts_as_published_once_it_is_written()
    {
        Assert.False(await Publisher.IsPublishedAsync(Publication, CancellationToken.None));

        await Publisher.PublishAsync(Publication, CancellationToken.None);

        Assert.True(await Publisher.IsPublishedAsync(Publication, CancellationToken.None));
    }

    [Fact]
    public async Task Saving_is_logged_in_the_run_log()
    {
        using var runLog = new RunLogCapture();
        var logger = runLog.CreateLogger<SavedReviewPublisher>();
        var publisher = new SavedReviewPublisher(_root.Path, logger);

        using (logger.BeginReviewRunScope(_runId))
        {
            await publisher.PublishAsync(Publication, CancellationToken.None);
        }

        var line = Assert.Single(runLog.Lines());
        Assert.Equal(_runId, line.RunId);
        Assert.Equal("Information", line.Level);
        Assert.Equal($"Saved the review of run {_runId} to {PublishedFile}, comments: 1.", line.Message);
    }

    public void Dispose() => _root.Dispose();
}
