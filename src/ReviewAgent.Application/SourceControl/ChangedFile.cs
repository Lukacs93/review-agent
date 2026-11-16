namespace ReviewAgent.Application.SourceControl;

/// <summary>
/// A file the pull request changes. <see cref="Patch"/> is its diff in unified format, starting at the first
/// <c>@@</c> hunk header, as GitHub returns it.
/// </summary>
public sealed record ChangedFile(string Path, FileChangeStatus Status, string Patch);
