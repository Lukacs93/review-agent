using ReviewAgent.Application.SourceControl;
using ReviewAgent.Application.SourceControl.Errors;

namespace ReviewAgent.Application.Tests.SourceControl;

public sealed class DiffLinesTests
{
    [Fact]
    public void Added_and_unchanged_lines_are_numbered_from_the_block_header()
    {
        const string patch =
            "@@ -1,4 +1,5 @@\n using A;\n+using B;\n using C;\n-using D;\n+using E;\n\\ No newline at end of file";

        var lines = LineNumbersInNewFile(patch);

        int[] expected = [1, 2, 3, 4];
        Assert.Equal(expected, lines);
    }

    [Fact]
    public void Every_change_block_starts_at_its_own_line()
    {
        const string patch = "@@ -1,2 +1,2 @@\n first\n+second\n@@ -10,2 +20,2 @@ class Order\n context\n+added";

        var lines = LineNumbersInNewFile(patch);

        int[] expected = [1, 2, 20, 21];
        Assert.Equal(expected, lines);
    }

    [Fact]
    public void Windows_line_endings_are_read_the_same()
    {
        var lines = LineNumbersInNewFile("@@ -0,0 +1,2 @@\r\n+one\r\n+two\r\n");

        int[] expected = [1, 2];
        Assert.Equal(expected, lines);
    }

    [Fact]
    public void Every_diff_line_is_kept_with_its_number_in_the_new_file()
    {
        const string patch = "@@ -8,3 +10,3 @@\n context\n-removed\n+added\n\\ No newline at end of file";

        var lines = DiffLines.Number(patch);

        NumberedDiffLine[] expected =
        [
            new("@@ -8,3 +10,3 @@", NewFileLine: null),
            new(" context", NewFileLine: 10),
            new("-removed", NewFileLine: null),
            new("+added", NewFileLine: 11),
            new("\\ No newline at end of file", NewFileLine: null),
        ];
        Assert.Equal(expected, lines);
    }

    [Fact]
    public void Empty_patch_has_no_lines() => Assert.Empty(DiffLines.Number(string.Empty));

    [Fact]
    public void Diff_without_a_block_header_is_rejected()
    {
        var error = Assert.Throws<FormatException>(() => DiffLines.Number("+added line\n unchanged line"));

        Assert.Equal(SourceControlErrors.UnreadableDiff, error.Message);
    }

    // The line numbers in the new file, in order: of the added and unchanged lines.
    private static List<int> LineNumbersInNewFile(string patch) =>
        DiffLines.Number(patch)
            .Where(diffLine => diffLine.NewFileLine.HasValue)
            .Select(diffLine => diffLine.NewFileLine.GetValueOrDefault())
            .ToList();
}
