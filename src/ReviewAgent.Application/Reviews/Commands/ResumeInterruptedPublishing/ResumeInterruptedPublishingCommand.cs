namespace ReviewAgent.Application.Reviews.Commands.ResumeInterruptedPublishing;

/// <summary>
/// Schedules again the runs that were being published when the application stopped. Run once at startup: the
/// publishing worker checks whether each review already went out, so none is published twice.
/// </summary>
/// <remarks>
/// It has no data: it always schedules every run being published. The type is still needed, because it names the
/// operation, and dependency injection finds its handler by it.
/// </remarks>
public sealed record ResumeInterruptedPublishingCommand;
