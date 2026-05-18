using ClearPilot.Core.Localization;

namespace ClearPilot.Core.Settings;

public sealed class AppSettings
{
    public const int DefaultLogRetentionDays = 7;
    public const int MinimumLogRetentionDays = 1;
    public const int MaximumLogRetentionDays = 365;

    public Language Language { get; set; } = Language.English;

    public int LogRetentionDays { get; set; } = DefaultLogRetentionDays;

    public bool AutoEmptyRecycleBin { get; set; }

    public bool DryRun { get; set; }

    public void Normalize()
    {
        if (LogRetentionDays < MinimumLogRetentionDays)
        {
            LogRetentionDays = MinimumLogRetentionDays;
        }

        if (LogRetentionDays > MaximumLogRetentionDays)
        {
            LogRetentionDays = MaximumLogRetentionDays;
        }

        if (!Enum.IsDefined(Language))
        {
            Language = Language.English;
        }
    }
}
