using ClearPilot.Core.Rules;

namespace ClearPilot.Core.Scanning;

public sealed record CleanupFileCandidate(
    CleanupRule Rule,
    string RootPath,
    string FilePath,
    long SizeBytes);
