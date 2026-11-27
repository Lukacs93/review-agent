namespace ReviewAgent.Evaluation;

/// <summary>
/// The GitHub Actions job summary, shown on the workflow run's page: Markdown appended to the file GitHub gives the
/// job. Outside GitHub Actions there is no such file, and nothing is written.
/// </summary>
internal static class StepSummary
{
    private const string FileVariable = "GITHUB_STEP_SUMMARY";

    public static async Task AppendAsync(string markdown, CancellationToken cancellationToken)
    {
        if (Environment.GetEnvironmentVariable(FileVariable) is { Length: > 0 } file)
        {
            await File.AppendAllTextAsync(file, markdown + Environment.NewLine, cancellationToken);
        }
    }
}
