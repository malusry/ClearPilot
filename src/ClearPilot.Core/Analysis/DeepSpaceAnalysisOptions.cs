namespace ClearPilot.Core.Analysis;

public sealed class DeepSpaceAnalysisOptions
{
    public IReadOnlyList<string> RootPaths { get; init; } = [];

    public IReadOnlyList<string> ExcludePathSegments { get; init; } = [];

    public long LargeFileThresholdBytes { get; init; } = 100L * 1024 * 1024;

    public long LargeFolderThresholdBytes { get; init; } = 500L * 1024 * 1024;

    public long FileTypeSummaryThresholdBytes { get; init; } = 100L * 1024 * 1024;

    public TimeSpan OldArchiveAge { get; init; } = TimeSpan.FromDays(30);

    public int MaxDepth { get; init; } = 6;

    public int MaxResults { get; init; } = 80;

    public int MaxLargeFiles { get; init; } = 20;

    public int MaxLargeFolders { get; init; } = 20;

    public int MaxOldArchivesAndInstallers { get; init; } = 20;

    public int MaxProjectDependencyFolders { get; init; } = 20;

    public int MaxFileTypeSummaries { get; init; } = 20;
}
