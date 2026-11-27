using System.Globalization;
using ReviewAgent.Evaluation.Dataset;

namespace ReviewAgent.Evaluation.Tests.Dataset;

public sealed class EvaluationDatasetTests : IDisposable
{
    private const string FirstTask =
        "{\"id\":\"task-01\",\"group\":\"positive\",\"repository\":\"owner/demo\",\"baseCommit\":\"abc\","
        + "\"headBranch\":\"feature/task-01\",\"title\":\"Add an endpoint\",\"description\":\"Adds one.\","
        + "\"patch\":\"\",\"expected\":[{\"rule\":\"ARCH-01\",\"file\":\"src/A.cs\","
        + "\"lines\":[{\"number\":3,\"code\":\"db\"}],\"optional\":false,\"reason\":null}]}";

    private const string SecondTask =
        "{\"id\":\"task-02\",\"group\":\"negative\",\"repository\":\"owner/demo\",\"baseCommit\":\"abc\","
        + "\"headBranch\":\"feature/task-02\",\"title\":\"Add a query\",\"description\":\"Adds one.\","
        + "\"patch\":\"\",\"expected\":[]}";

    private readonly string _file = Path.Combine(
        Path.GetTempPath(),
        $"dataset-{Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)}.jsonl");

    public void Dispose() => File.Delete(_file);

    [Fact]
    public async Task Every_line_is_a_task()
    {
        await File.WriteAllTextAsync(_file, $"{FirstTask}\n{SecondTask}\n");

        var tasks = await EvaluationDataset.LoadAsync(_file, CancellationToken.None);

        Assert.Equal(2, tasks.Count);
        Assert.Equal(TaskGroup.Positive, tasks[0].Group);
        var line = Assert.Single(Assert.Single(tasks[0].Expected).Lines);
        Assert.Equal(3, line.Number);
        Assert.Equal(TaskGroup.Negative, tasks[1].Group);
    }

    // A result is reused only for the same version of its task.
    [Fact]
    public async Task Changed_task_gets_another_fingerprint_and_the_others_keep_theirs()
    {
        await File.WriteAllTextAsync(_file, $"{FirstTask}\n{SecondTask}\n");
        var before = await EvaluationDataset.LoadAsync(_file, CancellationToken.None);

        var corrected = FirstTask.Replace("\"number\":3", "\"number\":4", StringComparison.Ordinal);
        await File.WriteAllTextAsync(_file, $"{corrected}\n{SecondTask}\n");
        var after = await EvaluationDataset.LoadAsync(_file, CancellationToken.None);

        Assert.NotEqual(before[0].Fingerprint, after[0].Fingerprint);
        Assert.Equal(before[1].Fingerprint, after[1].Fingerprint);
    }
}
