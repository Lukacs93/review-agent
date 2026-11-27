using System.Diagnostics;
using System.Text;

namespace ReviewAgent.Evaluation.Pulls;

/// <summary>Runs the git command line, which must be on the PATH.</summary>
internal static class Git
{
    private static readonly UTF8Encoding _utf8 = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>Runs git and returns what it wrote; throws with git's error message when it fails.</summary>
    public static Task<string> RunAsync(
        string workingDirectory,
        CancellationToken cancellationToken,
        params string[] arguments) =>
        RunWithInputAsync(workingDirectory, input: null, cancellationToken, arguments);

    /// <summary>Runs git with <paramref name="input"/> on its standard input, for example a patch to apply.</summary>
    public static async Task<string> RunWithInputAsync(
        string workingDirectory,
        string? input,
        CancellationToken cancellationToken,
        params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardInput = input is not null,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = input is null ? null : _utf8,
            StandardOutputEncoding = _utf8,
            StandardErrorEncoding = _utf8,
            UseShellExecute = false,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException(EvaluationErrors.GitNotStarted);

        // Read both outputs while writing the input, so neither side waits for the other.
        var output = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var error = process.StandardError.ReadToEndAsync(cancellationToken);
        if (input is not null)
        {
            await process.StandardInput.WriteAsync(input.AsMemory(), cancellationToken);
            process.StandardInput.Close();
        }

        await process.WaitForExitAsync(cancellationToken);
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                EvaluationErrors.GitFailed(string.Join(' ', arguments), (await error).Trim()));
        }

        return await output;
    }
}
