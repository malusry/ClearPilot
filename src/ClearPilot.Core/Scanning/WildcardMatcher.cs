using System.Text.RegularExpressions;

namespace ClearPilot.Core.Scanning;

internal static class WildcardMatcher
{
    public static bool IsMatch(string value, string pattern)
    {
        var expression = "^" + Regex.Escape(pattern)
            .Replace("\\*", ".*", StringComparison.Ordinal)
            .Replace("\\?", ".", StringComparison.Ordinal) + "$";

        return Regex.IsMatch(value, expression, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }
}
