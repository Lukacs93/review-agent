using System.Globalization;
using ReviewAgent.Application.SourceControl.Errors;

namespace ReviewAgent.Application.SourceControl;

/// <summary>
/// Reads the line numbers of a diff: which line of the changed file each diff line is. They tell where a review
/// comment may be placed (GitHub rejects a comment on any other line), and they are shown to the agent next to the
/// diff, so it reads the line numbers instead of counting them.
/// </summary>
/// <remarks>
/// A diff shows a file's changes in blocks. Each block starts with a header line; the number after the
/// <c>+</c> is the line of the changed file where the block starts. Every other line starts with a sign:
/// <c>+</c> for an added line, a space for an unchanged line, <c>-</c> for a removed line. Added and unchanged
/// lines are in the changed file, so they are counted from the block's first line; removed lines are not, so
/// they get no number.
/// <code>
/// @@ -8,2 +10,3 @@                  header: this block starts at line 10
///  public class OrdersController(   unchanged  → line 10
/// -    CancelOrderHandler cancel)   removed    → no line number
/// +    CancelOrderHandler cancel,   added      → line 11
/// +    OrdersDbContext db)          added      → line 12
/// </code>
/// For this block the lines in the new file are 10, 11 and 12.
/// </remarks>
public static class DiffLines
{
    private const char AddedLineMarker = '+';
    private const char UnchangedLineMarker = ' ';

    /// <summary>
    /// The line numbers in the new file of the added and unchanged lines the diff shows. Throws
    /// <see cref="FormatException"/> when the diff has content but no block header.
    /// </summary>
    public static IReadOnlySet<int> InNewFile(string patch) =>
        Number(patch)
            .Select(diffLine => diffLine.NewFileLine)
            .OfType<int>() // only the lines that have a number in the new file
            .ToHashSet();

    /// <summary>
    /// Every line of the diff, in order, with its line number in the new file: added and unchanged lines have one;
    /// block headers, removed lines and notes have none. Throws <see cref="FormatException"/> when the diff has
    /// content but no block header.
    /// </summary>
    public static IReadOnlyList<NumberedDiffLine> Number(string patch)
    {
        ArgumentNullException.ThrowIfNull(patch);

        var numberedLines = new List<NumberedDiffLine>();

        // The line number in the new file of the next added or unchanged line. Null until the first block header:
        // lines before it belong to no block.
        int? nextNewFileLine = null;

        foreach (var diffLine in SplitLines(patch))
        {
            if (diffLine.Length == 0)
            {
                continue;
            }

            if (TryReadBlockStart(diffLine, out var blockStart))
            {
                nextNewFileLine = blockStart;
                numberedLines.Add(new NumberedDiffLine(diffLine, NewFileLine: null));
                continue;
            }

            if (nextNewFileLine is null)
            {
                continue;
            }

            // Only added and unchanged lines are in the new file, so only they get a line number there. Removed
            // lines ('-') and the "\ No newline at end of file" note get none, and are not counted.
            var marker = diffLine.First();
            if (marker is AddedLineMarker or UnchangedLineMarker)
            {
                numberedLines.Add(new NumberedDiffLine(diffLine, nextNewFileLine));
                nextNewFileLine++;
            }
            else
            {
                numberedLines.Add(new NumberedDiffLine(diffLine, NewFileLine: null));
            }
        }

        // A diff with content but no block header is not in the format this code reads. Fail loudly instead of
        // returning no lines, which would silently reject every comment on the file.
        if (nextNewFileLine is null && !string.IsNullOrWhiteSpace(patch))
        {
            throw new FormatException(SourceControlErrors.UnreadableDiff);
        }

        return numberedLines;
    }

    /// <summary>
    /// Reads a block header such as <c>@@ -8,2 +10,3 @@</c> and returns the line of the changed file where the
    /// block starts, here 10.
    /// </summary>
    private static bool TryReadBlockStart(string diffLine, out int newFileStart)
    {
        const string BlockHeaderStart = "@@ -";

        // In "@@ -8,2 +10,3 @@" split at the spaces, the changed file's part ("+10,3") is the third, index 2.
        const int ChangedFilePartIndex = 2;

        newFileStart = 0;

        // Every block header starts with "@@ -"; any other line is not a header.
        if (!diffLine.StartsWith(BlockHeaderStart, StringComparison.Ordinal))
        {
            return false;
        }

        // The header's parts between the spaces: "@@", "-8,2", "+10,3", "@@".
        var parts = diffLine.Split(' ');
        if (parts.Length <= ChangedFilePartIndex)
        {
            return false;
        }

        // The third part describes the changed file: "+10,3" means from line 10, 3 lines.
        var changedFilePart = parts[ChangedFilePartIndex]; // "+10,3"
        var withoutPlus = changedFilePart.TrimStart(AddedLineMarker); // "10,3"
        var firstLineText = withoutPlus.Split(',')[0]; // "10" (a one-line block has no comma: "+10")

        return int.TryParse(firstLineText, NumberStyles.None, CultureInfo.InvariantCulture, out newFileStart);
    }

    // GitHub ends lines with "\n". A saved pull request edited on Windows may end them with "\r\n";
    // the "\r" left at the end of each line is removed.
    private static IEnumerable<string> SplitLines(string patch) =>
        patch.Split('\n').Select(line => line.TrimEnd('\r'));
}
