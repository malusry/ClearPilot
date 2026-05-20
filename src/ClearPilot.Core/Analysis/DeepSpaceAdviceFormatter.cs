using ClearPilot.Core.Localization;

namespace ClearPilot.Core.Analysis;

public static class DeepSpaceAdviceFormatter
{
    public static string FormatExplanation(Language language, DeepSpaceItem item)
    {
        if (language != Language.SimplifiedChinese)
        {
            return item.Explanation;
        }

        if (item.AdviceKey == DeepSpaceAdviceKey.WindowsSystemManagedArea)
        {
            return IsGameLauncherReviewItem(item)
                ? "这是游戏启动器相关的复核项。ClearPilot 仅分析，不删除。"
                : "这是 Windows 系统管理区域。ClearPilot 仅分析，不删除。";
        }

        return item.AdviceKey switch
        {
            DeepSpaceAdviceKey.NodeModules => "Node.js 项目依赖目录，通常可重建，但会影响下一次安装或构建速度。",
            DeepSpaceAdviceKey.PythonVirtualEnvironment => "Python 虚拟环境，通常可重建，但会影响下一次环境准备时间。",
            DeepSpaceAdviceKey.FrontendFrameworkOutput or DeepSpaceAdviceKey.FrontendBuildCache => "前端构建输出或缓存，通常可重建，但会影响下一次启动或构建速度。",
            DeepSpaceAdviceKey.ProjectBuildOutput or DeepSpaceAdviceKey.DotNetBuildOutput or DeepSpaceAdviceKey.TargetBuildOutput => "项目构建输出目录，通常可重建，但可能包含仍需保留的产物。",
            DeepSpaceAdviceKey.GenericProjectDependency => "项目依赖或工程状态目录，请先确认项目工作流后再处理。",
            DeepSpaceAdviceKey.VideoFile => "大型视频文件，通常属于个人数据而非缓存。",
            DeepSpaceAdviceKey.DiskImage => "旧磁盘镜像，可能仍用于安装、恢复或归档。",
            DeepSpaceAdviceKey.Archive or DeepSpaceAdviceKey.GenericArchiveOrInstaller => "旧压缩包或安装资源，可能包含仍需保留的数据。",
            DeepSpaceAdviceKey.Installer => "旧安装包，若可重新下载通常可后续手动处理。",
            DeepSpaceAdviceKey.LargeFolder => "大型目录，可能混合包含项目数据或个人文件。",
            DeepSpaceAdviceKey.GenericLargeFile => "大型文件，可能是个人或项目重要数据。",
            DeepSpaceAdviceKey.GenericFileTypeSummary => "该文件类型在扫描根中占用较大空间。",
            _ => item.Explanation
        };
    }

    public static string FormatSuggestedAction(Language language, DeepSpaceItem item)
    {
        if (language != Language.SimplifiedChinese)
        {
            return item.SuggestedAction;
        }

        if (item.AdviceKey == DeepSpaceAdviceKey.WindowsSystemManagedArea)
        {
            return IsGameLauncherReviewItem(item)
                ? "先复核大小和近期活动，再使用启动器自带维护流程处理；不要直接删除。"
                : "优先使用 Windows 设置、存储感知或磁盘清理，不要直接删除系统管理区域。";
        }

        return item.AdviceKey switch
        {
            DeepSpaceAdviceKey.VideoFile => "先手动确认内容，优先考虑归档或迁移，而不是直接删除。",
            DeepSpaceAdviceKey.DiskImage or DeepSpaceAdviceKey.Archive or DeepSpaceAdviceKey.Installer => "先人工复核内容和用途，再决定是否手动处理。",
            _ => "先人工复核，再决定是否手动处理。"
        };
    }

    public static string FormatPossibleImpact(Language language, DeepSpaceItem item, string possibleImpact)
    {
        if (language != Language.SimplifiedChinese)
        {
            return possibleImpact;
        }

        if (item.AdviceKey == DeepSpaceAdviceKey.WindowsSystemManagedArea)
        {
            return IsGameLauncherReviewItem(item)
                ? "处理后可能触发着色器重编译、下载恢复异常或启动器状态变化。"
                : "手动处理可能影响 Windows 更新、诊断或维护状态。";
        }

        return item.AdviceKey switch
        {
            DeepSpaceAdviceKey.NodeModules or DeepSpaceAdviceKey.PythonVirtualEnvironment or DeepSpaceAdviceKey.FrontendFrameworkOutput
                => "通常会在后续运行中重建，但首次运行可能更慢。",
            DeepSpaceAdviceKey.VideoFile or DeepSpaceAdviceKey.DiskImage
                => "若无可靠备份，处理后可能无法恢复原始内容。",
            _ => "影响取决于所属应用或工作流，请先确认后再手动处理。"
        };
    }

    public static string FormatSafetyNote(Language language, DeepSpaceItem item, string safetyNote)
    {
        if (language != Language.SimplifiedChinese)
        {
            return safetyNote;
        }

        if (item.AdviceKey == DeepSpaceAdviceKey.WindowsSystemManagedArea)
        {
            return IsGameLauncherReviewItem(item)
                ? "仅分析，不清理。请勿在下载或更新进行中处理该目录。"
                : "仅分析，不清理。请通过系统官方工具处理。";
        }

        return item.AdviceKey switch
        {
            DeepSpaceAdviceKey.VideoFile or DeepSpaceAdviceKey.DiskImage or DeepSpaceAdviceKey.Archive or DeepSpaceAdviceKey.GenericArchiveOrInstaller
                => "仅分析，不清理。该类文件可能是用户数据，请谨慎复核。",
            DeepSpaceAdviceKey.NodeModules or DeepSpaceAdviceKey.PythonVirtualEnvironment or DeepSpaceAdviceKey.FrontendFrameworkOutput
                => "仅分析，不清理。请在确认可重建且当前无活跃任务时再处理。",
            _ => "仅分析，不清理。ClearPilot 不会自动删除该项。"
        };
    }

    private static bool IsGameLauncherReviewItem(DeepSpaceItem item)
    {
        return item.Type == DeepSpaceItemType.GameLauncherReviewArea
            || item.TargetId.Contains("steam-shadercache", StringComparison.OrdinalIgnoreCase)
            || item.TargetId.Contains("steam-depotcache", StringComparison.OrdinalIgnoreCase);
    }
}
