using System.ComponentModel.DataAnnotations;

namespace ReviewAgent.Infrastructure.Logging;

/// <summary>The <c>RunLog</c> section of the configuration: the review runs' log lines saved to the database.</summary>
internal sealed class RunLogOptions
{
    public const string SectionName = "RunLog";

    /// <summary>
    /// How many log lines may wait to be saved. When the queue is full, the oldest line is dropped and the drop is
    /// logged.
    /// </summary>
    // Only a limit against running out of memory while the database does not answer: the lines are saved as soon as
    // they arrive, so the queue is normally almost empty. The default is the queue length of .NET's own console
    // logger (ConsoleLoggerOptions.MaxQueueLength); settable, since the right limit depends on the memory.
    [Range(1, int.MaxValue)]
    public int QueueCapacity { get; init; } = 2500;
}
