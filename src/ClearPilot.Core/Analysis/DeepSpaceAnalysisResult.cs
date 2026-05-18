namespace ClearPilot.Core.Analysis;

public sealed record DeepSpaceAnalysisResult(
    IReadOnlyList<DeepSpaceItem> Items,
    DeepSpaceAnalysisSummary Summary);
