using System.Diagnostics;

namespace ClearPilot.Core.Cleanup;

public sealed class SystemProcessInspector : IProcessInspector
{
    public bool IsAnyRunning(IReadOnlyList<string> processNames)
    {
        if (processNames.Count == 0)
        {
            return false;
        }

        var expectedNames = processNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(NormalizeName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (expectedNames.Count == 0)
        {
            return false;
        }

        try
        {
            foreach (var process in Process.GetProcesses())
            {
                try
                {
                    if (expectedNames.Contains(NormalizeName(process.ProcessName)))
                    {
                        return true;
                    }
                }
                catch (InvalidOperationException)
                {
                }
                finally
                {
                    process.Dispose();
                }
            }
        }
        catch (InvalidOperationException)
        {
            return true;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return true;
        }

        return false;
    }

    private static string NormalizeName(string name)
    {
        var trimmed = name.Trim();
        return trimmed.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? trimmed[..^4]
            : trimmed;
    }
}
