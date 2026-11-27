using System.Globalization;
using ReviewAgent.Evaluation.Measuring;

namespace ReviewAgent.Evaluation.Tests.Measuring;

public sealed class ExecutionLockTests : IDisposable
{
    private readonly string _resultsDirectory = Path.Combine(
        Path.GetTempPath(),
        "review-agent-evaluation-test-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));

    public void Dispose() => TemporaryFolder.Delete(_resultsDirectory);

    [Fact]
    public void Measurement_runs_in_one_process_at_a_time()
    {
        using var first = ExecutionLock.TryAcquire(_resultsDirectory, "baseline");

        using var second = ExecutionLock.TryAcquire(_resultsDirectory, "baseline");

        Assert.NotNull(first);
        Assert.Null(second);
    }

    [Fact]
    public void Released_lock_can_be_taken_again()
    {
        ExecutionLock.TryAcquire(_resultsDirectory, "baseline")!.Dispose();

        using var again = ExecutionLock.TryAcquire(_resultsDirectory, "baseline");

        Assert.NotNull(again);
    }

    [Fact]
    public void Other_measurements_are_not_locked()
    {
        using var baseline = ExecutionLock.TryAcquire(_resultsDirectory, "baseline");

        using var other = ExecutionLock.TryAcquire(_resultsDirectory, "after-prompt-fix");

        Assert.NotNull(other);
    }
}
