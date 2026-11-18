using System.Globalization;
using ReviewAgent.Application.SourceControl;

namespace ReviewAgent.McpServer.Tools;

/// <summary>
/// Puts the line number in front of each line of a diff or a file, so the model reads the line of a review comment
/// instead of counting it, which it often gets wrong by one.
/// </summary>
/// <remarks>
/// <code>
///    | @@ -9,6 +9,8 @@
///  9 |      public Guid CustomerId { get; private set; }
/// 12 | +    public DateTimeOffset? ShippedAt { get; private set; }
///    | -    public decimal Discount;
/// </code>
/// The numbers are aligned to the right, so the code after them stays in one column.
/// </remarks>
internal static class NumberedText
{
    // Between the line number and the line.
    private const string NumberSeparator = " | ";

    /// <summary>
    /// The diff with the line number in the new file in front of each added or unchanged line; block headers and
    /// removed lines get an empty number column.
    /// </summary>
    public static string Diff(string patch)
    {
        var lines = DiffLines.Number(patch);
        var highestNumber = lines.Select(line => line.NewFileLine).OfType<int>().DefaultIfEmpty(0).Max();
        var numberWidth = DigitCount(highestNumber);

        return string.Join('\n', lines.Select(line => NumberColumn(line.NewFileLine, numberWidth) + line.Text));
    }

    /// <summary>The file with its line number, starting from 1, in front of each line.</summary>
    public static string File(string content)
    {
        ArgumentNullException.ThrowIfNull(content);

        // A file ending with a line break has no line after it; splitting would give an empty last item.
        var lines = content.Split('\n').Select(line => line.TrimEnd('\r')).ToList();
        if (lines.Count > 1 && lines.Last().Length == 0)
        {
            lines.RemoveAt(lines.Count - 1);
        }

        var numberWidth = DigitCount(lines.Count);
        return string.Join('\n', lines.Select((line, index) => NumberColumn(index + 1, numberWidth) + line));
    }

    private static string NumberColumn(int? lineNumber, int width)
    {
        var number = lineNumber?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        return number.PadLeft(width) + NumberSeparator;
    }

    private static int DigitCount(int number) => number.ToString(CultureInfo.InvariantCulture).Length;
}
