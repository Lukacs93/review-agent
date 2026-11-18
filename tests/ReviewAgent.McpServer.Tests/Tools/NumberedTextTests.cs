using ReviewAgent.McpServer.Tools;

namespace ReviewAgent.McpServer.Tests.Tools;

public sealed class NumberedTextTests
{
    [Fact]
    public void Diff_lines_without_a_line_number_get_an_empty_number_column()
    {
        const string patch = "@@ -9,2 +9,2 @@\n context\n-removed\n+added";

        var numbered = NumberedText.Diff(patch);

        string[] expected =
        [
            "   | @@ -9,2 +9,2 @@",
            " 9 |  context",
            "   | -removed",
            "10 | +added",
        ];
        Assert.Equal(string.Join('\n', expected), numbered);
    }

    [Fact]
    public void File_lines_are_numbered_from_one()
    {
        var numbered = NumberedText.File("first\r\nsecond\n");

        Assert.Equal("1 | first\n2 | second", numbered);
    }

    [Fact]
    public void Line_numbers_are_aligned_to_the_right()
    {
        var content = string.Join('\n', Enumerable.Range(1, 10).Select(number => $"line {number}"));

        var lines = NumberedText.File(content).Split('\n');

        Assert.Equal(" 1 | line 1", lines.First());
        Assert.Equal("10 | line 10", lines.Last());
    }
}
