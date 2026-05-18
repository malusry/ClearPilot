namespace ClearPilot.Core.Analysis;

public sealed record DeepSpaceAnalysisSummary(
    int ScannedRootCount,
    int ScannedDirectoryCount,
    int ScannedFileCount,
    int FindingCount,
    long FindingBytes);
