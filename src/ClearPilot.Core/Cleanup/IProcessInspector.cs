namespace ClearPilot.Core.Cleanup;

public interface IProcessInspector
{
    bool IsAnyRunning(IReadOnlyList<string> processNames);
}
