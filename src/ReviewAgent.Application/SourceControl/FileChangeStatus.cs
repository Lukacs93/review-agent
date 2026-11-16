namespace ReviewAgent.Application.SourceControl;

/// <summary>What the pull request does to a file.</summary>
public enum FileChangeStatus
{
    Added,
    Modified,
    Removed,
    Renamed,
}
