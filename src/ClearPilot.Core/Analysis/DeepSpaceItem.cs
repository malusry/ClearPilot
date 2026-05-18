using ClearPilot.Core.Cleanup;

namespace ClearPilot.Core.Analysis;

public sealed record DeepSpaceItem(
    DeepSpaceItemType Type,
    string Path,
    long SizeBytes,
    DateTimeOffset? LastWriteTime,
    RiskLevel RiskLevel,
    string Explanation,
    string SuggestedAction,
    DeepSpaceAdviceKey AdviceKey = DeepSpaceAdviceKey.GenericLargeFile,
    string AdviceSubject = "");
