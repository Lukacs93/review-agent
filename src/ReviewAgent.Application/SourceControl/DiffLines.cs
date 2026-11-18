using System.Globalization;
using ReviewAgent.Application.SourceControl.Errors;

namespace ReviewAgent.Application.SourceControl;

/// <summary>
/// Finds the line numbers a review comment may be placed on: the lines of the changed file that its diff shows.
/// GitHub rejects a comment on any other line, so the agent's comments are checked against these.
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
/// For this block the result is 10, 11 and 12.
/// </remarks>
public static class DiffLines
{
    private const char AddedLineMarker = '+';
    private const char UnchangedLineMarker = ' ';

    /// <summary>
    /// The line numbers in the new file of the added and unchanged lines the diff shows. Throws
    /// <see cref="FormatException"/> when the diff has content but no block header.
    /// </summary>
    public static IReadOnlySet<int> InNewFile(string patch)
    {
        ArgumentNullException.ThrowIfNull(patch);

        var linesInNewFile = new HashSet<int>();

        // The line number in the new file of the next added or unchanged line. Null until the first block header:
        // lines before it belong to no block.
        int? nextNewFileLine = null;

        foreach (var diffLine in SplitLines(patch))
        {
            if (TryReadBlockStart(diffLine, out var blockStart))
            {
                nextNewFileLine = blockStart;
                continue;
            }

            if (nextNewFileLine is null || diffLine.Length == 0)
            {
                continue;
            }

            // Only added and unchanged lines are in the new file, so only they get a line number there. Removed
            // lines ('-') and the "\ No newline at end of file" note are skipped without counting.
            var marker = diffLine[0];
            if (marker is AddedLineMarker or UnchangedLineMarker)
            {
                linesInNewFile.Add(nextNewFileLine.Value);
                nextNewFileLine++;
            }
        }

        // A diff with content but no block header is not in the format this code reads. Fail loudly instead of
        // returning no lines, which would silently reject every comment on the file.
        if (nextNewFileLine is null && !string.IsNullOrWhiteSpace(patch))
        {
            throw new FormatException(SourceControlErrors.UnreadableDiff);
        }

        return linesInNewFile;
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
