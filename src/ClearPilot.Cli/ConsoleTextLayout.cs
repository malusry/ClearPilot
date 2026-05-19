using System.Text;

namespace ClearPilot.Cli;

public static class ConsoleTextLayout
{
    public static string FitCell(string value, int width, bool alignRight = false)
    {
        var truncated = TruncateToDisplayWidth(value, width);
        var padding = width - GetTextDisplayWidth(truncated);
        if (padding <= 0)
        {
            return truncated;
        }

        return alignRight
            ? new string(' ', padding) + truncated
            : truncated + new string(' ', padding);
    }

    public static string TruncateToDisplayWidth(string value, int maxWidth)
    {
        if (maxWidth <= 0 || GetTextDisplayWidth(value) <= maxWidth)
        {
            return value;
        }

        var builder = new StringBuilder();
        var width = 0;
        var ellipsisWidth = 1;

        foreach (var rune in value.EnumerateRunes())
        {
            var runeWidth = GetRuneDisplayWidth(rune);
            if (width + runeWidth + ellipsisWidth > maxWidth)
            {
                break;
            }

            builder.Append(rune.ToString());
            width += runeWidth;
        }

        builder.Append('…');
        return builder.ToString();
    }

    public static IReadOnlyList<string> WrapToDisplayWidth(string value, int maxWidth)
    {
        if (maxWidth <= 0 || string.IsNullOrEmpty(value))
        {
            return [string.Empty];
        }

        var lines = new List<string>();
        var builder = new StringBuilder();
        var width = 0;

        foreach (var rune in value.EnumerateRunes())
        {
            var runeText = rune.ToString();
            var runeWidth = GetRuneDisplayWidth(rune);

            if (width > 0 && width + runeWidth > maxWidth)
            {
                lines.Add(builder.ToString());
                builder.Clear();
                width = 0;
            }

            builder.Append(runeText);
            width += runeWidth;
        }

        if (builder.Length > 0 || lines.Count == 0)
        {
            lines.Add(builder.ToString());
        }

        return lines;
    }

    public static int GetTextDisplayWidth(string value)
    {
        var width = 0;
        foreach (var rune in value.EnumerateRunes())
        {
            width += GetRuneDisplayWidth(rune);
        }

        return width;
    }

    public static int GetRuneDisplayWidth(Rune rune)
    {
        var value = rune.Value;
        if (value == 0)
        {
            return 0;
        }

        if (value < 32 || value is >= 0x7F and < 0xA0)
        {
            return 0;
        }

        return IsWideRune(value) ? 2 : 1;
    }

    public static IReadOnlyList<WrappedHighlightedDetailLine> WrapHighlightedDetail(
        string prefix,
        string highlight,
        string suffix,
        int maxWidth)
    {
        prefix ??= string.Empty;
        highlight ??= string.Empty;
        suffix ??= string.Empty;

        if (maxWidth <= 0)
        {
            return [new WrappedHighlightedDetailLine(prefix, highlight + suffix)];
        }

        var body = highlight + suffix;
        if (string.IsNullOrEmpty(prefix))
        {
            return WrapToDisplayWidth(body, maxWidth)
                .Select(line => new WrappedHighlightedDetailLine(string.Empty, line))
                .ToArray();
        }

        var prefixWidth = GetTextDisplayWidth(prefix);
        if (prefixWidth >= maxWidth)
        {
            var lines = new List<WrappedHighlightedDetailLine>();
            foreach (var prefixLine in WrapToDisplayWidth(prefix, maxWidth))
            {
                lines.Add(new WrappedHighlightedDetailLine(prefixLine, string.Empty));
            }

            if (!string.IsNullOrEmpty(body))
            {
                foreach (var bodyLine in WrapToDisplayWidth(body, maxWidth))
                {
                    lines.Add(new WrappedHighlightedDetailLine(string.Empty, bodyLine));
                }
            }

            return lines;
        }

        var continuationPrefix = new string(' ', prefixWidth);
        var bodyWidth = Math.Max(1, maxWidth - prefixWidth);
        var bodyLines = WrapToDisplayWidth(body, bodyWidth);
        var wrappedLines = new List<WrappedHighlightedDetailLine>(bodyLines.Count);

        if (bodyLines.Count == 0)
        {
            wrappedLines.Add(new WrappedHighlightedDetailLine(prefix, string.Empty));
            return wrappedLines;
        }

        wrappedLines.Add(new WrappedHighlightedDetailLine(prefix, bodyLines[0]));
        for (var index = 1; index < bodyLines.Count; index++)
        {
            wrappedLines.Add(new WrappedHighlightedDetailLine(continuationPrefix, bodyLines[index]));
        }

        return wrappedLines;
    }

    private static bool IsWideRune(int value)
    {
        return value is
            >= 0x1100 and <= 0x115F or
            >= 0x2329 and <= 0x232A or
            >= 0x2E80 and <= 0xA4CF or
            >= 0xAC00 and <= 0xD7A3 or
            >= 0xF900 and <= 0xFAFF or
            >= 0xFE10 and <= 0xFE19 or
            >= 0xFE30 and <= 0xFE6F or
            >= 0xFF00 and <= 0xFF60 or
            >= 0xFFE0 and <= 0xFFE6;
    }
}

public readonly record struct WrappedHighlightedDetailLine(string Prefix, string Body);
