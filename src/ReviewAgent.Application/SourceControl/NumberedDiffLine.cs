namespace ReviewAgent.Application.SourceControl;

/// <summary>One line of a diff as it is, and its line number in the new file, if it has one.</summary>
public sealed record NumberedDiffLine(string Text, int? NewFileLine);
