namespace ReviewAgent.Evaluation.Measuring;

/// <summary>
/// Keeps a measurement to one process at a time, so two processes do not run the same reviews and write the same
/// results. The lock file is open for exclusive use for as long as the measurement runs; the operating system
/// releases it when the process ends in any way, so no lock is left behind.
/// </summary>
internal sealed class ExecutionLock : IDisposable
{
    private readonly FileStream _file;

    private ExecutionLock(FileStream file) => _file = file;

    /// <summary>The lock of the measurement; <see langword="null"/> when another process holds it.</summary>
    public static ExecutionLock? TryAcquire(string resultsDirectory, string name)
    {
        var path = Path.Combine(resultsDirectory, ExecutionRecord.ExecutionsDirectoryName, $"{name}.lock");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        try
        {
            return new ExecutionLock(new FileStream(
                path,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None,
                bufferSize: 1,
                FileOptions.DeleteOnClose));
        }
        catch (IOException)
        {
            return null;
        }
    }

    public void Dispose() => _file.Dispose();
}
