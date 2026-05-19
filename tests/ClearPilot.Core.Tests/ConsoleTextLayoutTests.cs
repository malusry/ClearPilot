using ClearPilot.Cli;
using Xunit;

namespace ClearPilot.Core.Tests;

public sealed class ConsoleTextLayoutTests
{
    [Fact]
    public void GetTextDisplayWidthTreatsChineseAsWide()
    {
        Assert.Equal(8, ConsoleTextLayout.GetTextDisplayWidth("安全说明"));
        Assert.Equal(6, ConsoleTextLayout.GetTextDisplayWidth("A安全B"));
    }

    [Fact]
    public void WrapToDisplayWidthPreservesFullChineseText()
    {
        const string text = "请使用 Windows 设置、存储感知或磁盘清理处理。";
        var lines = ConsoleTextLayout.WrapToDisplayWidth(text, maxWidth: 20);
        var merged = string.Concat(lines);

        Assert.True(lines.Count > 1);
        Assert.Equal(text, merged);
    }

    [Fact]
    public void WrapHighlightedDetailWrapsChineseWithoutDroppingCharacters()
    {
        const string prefix = "建议操作: ";
        const string highlight = "请使用 Windows 设置、存储感知或磁盘清理处理。";
        const string suffix = "  仅分析，不清理。";

        var lines = ConsoleTextLayout.WrapHighlightedDetail(prefix, highlight, suffix, maxWidth: 26);
        var reconstructed = Reconstruct(lines);

        Assert.True(lines.Count > 1);
        Assert.Equal(prefix + highlight + suffix, reconstructed);
        Assert.StartsWith(new string(' ', ConsoleTextLayout.GetTextDisplayWidth(prefix)), lines[1].Prefix);
    }

    [Fact]
    public void WrapHighlightedDetailWrapsSuffixWhenPresent()
    {
        const string prefix = "风险: ";
        const string highlight = "S2 REVIEW";
        const string suffix = "   类型: Windows 系统管理区域";

        var lines = ConsoleTextLayout.WrapHighlightedDetail(prefix, highlight, suffix, maxWidth: 24);
        var reconstructed = Reconstruct(lines);
        var mergedBody = string.Concat(lines.Select(line => line.Body));

        Assert.True(lines.Count > 1);
        Assert.Equal(prefix + highlight + suffix, reconstructed);
        Assert.Contains("系统管理区域", mergedBody, StringComparison.Ordinal);
    }

    private static string Reconstruct(IReadOnlyList<WrappedHighlightedDetailLine> lines)
    {
        if (lines.Count == 0)
        {
            return string.Empty;
        }

        var first = lines[0].Prefix + lines[0].Body;
        var remaining = string.Concat(lines.Skip(1).Select(line => line.Body));
        return first + remaining;
    }
}
