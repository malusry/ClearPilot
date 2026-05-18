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

        var subject = string.IsNullOrWhiteSpace(item.AdviceSubject) ? "此类型文件" : item.AdviceSubject;
        return item.AdviceKey switch
        {
            DeepSpaceAdviceKey.NodeModules => "Node.js 项目依赖目录。通常可以通过 package manifest 重新安装，但它可能很大且和项目状态相关。",
            DeepSpaceAdviceKey.PythonVirtualEnvironment => "Python 虚拟环境。通常可以从项目依赖文件重建，但本地解释器和包状态可能仍有价值。",
            DeepSpaceAdviceKey.TargetBuildOutput => "常见于 Rust、Maven 或类似工具的构建输出。通常可以重建，但之后构建可能变慢。",
            DeepSpaceAdviceKey.DotNetBuildOutput => ".NET 或编译型项目的构建输出。通常是生成内容，但删除后活跃工作区需要重新构建。",
            DeepSpaceAdviceKey.GradleProjectCache => "Gradle 项目缓存或构建状态。通常可以重建，但下一次 Gradle 运行可能更慢。",
            DeepSpaceAdviceKey.FrontendFrameworkOutput => "前端框架构建缓存或输出目录。通常是生成内容，但删除后下一次开发服务器或构建可能变慢。",
            DeepSpaceAdviceKey.FrontendBuildCache => "前端构建加速缓存。通常可以重建，但下一次构建可能变慢。",
            DeepSpaceAdviceKey.ProjectLocalCache => "项目本地缓存目录。它可能由构建工具生成，但需要先确认所属项目和工具。",
            DeepSpaceAdviceKey.PythonToolCache => "Python 工具缓存或测试环境状态。通常可以由创建它的工具重新生成。",
            DeepSpaceAdviceKey.TerraformWorkingDirectory => "Terraform 工作目录。它可能包含下载的 provider、module 和本地状态，删除前需要谨慎复查。",
            DeepSpaceAdviceKey.ProjectBuildOutput => "项目构建输出目录。它通常是生成内容，但也可能包含你主动保留的交付产物。",
            DeepSpaceAdviceKey.CoverageOutput => "测试覆盖率输出。通常可以通过重新运行测试生成。",
            DeepSpaceAdviceKey.VendorDependencies => "项目依赖目录。它可能可以由包管理器重建，但有些项目会故意保留 vendor 内容。",
            DeepSpaceAdviceKey.GenericProjectDependency => "项目依赖或构建输出目录。删除前需要手动复查，因为它可能影响项目工作区。",
            DeepSpaceAdviceKey.LargeFolder => "用户可控位置中的大目录。删除、归档或移动其中内容前需要手动复查。",
            DeepSpaceAdviceKey.VideoFile => "用户可控位置中的大型视频文件。视频通常是真实个人数据，不是缓存。",
            DeepSpaceAdviceKey.LogFile => "用户可控位置中的大型日志文件。日志可能对诊断有用，但旧日志也可能已经不需要。",
            DeepSpaceAdviceKey.TemporaryFile => "用户可控位置中的大型临时文件。文件名看起来像临时文件，但 ClearPilot 无法判断它属于哪个应用。",
            DeepSpaceAdviceKey.BackupFile => "用户可控位置中的大型备份文件。如果它是唯一备份副本，可能非常重要。",
            DeepSpaceAdviceKey.GenericLargeFile => "用户可控位置中的大文件。它可能是重要的个人数据或项目数据。",
            DeepSpaceAdviceKey.DiskImage => "旧磁盘镜像。它可能是安装镜像、系统镜像或仍然重要的归档介质。",
            DeepSpaceAdviceKey.Installer => "旧安装包。如果应用已安装且安装包可重新下载，它可能可以丢弃。",
            DeepSpaceAdviceKey.Archive => "旧压缩包。它可能包含用户文件、项目快照、导出文件或安装包内容。",
            DeepSpaceAdviceKey.GenericArchiveOrInstaller => "旧压缩包、磁盘镜像或安装包。它可能可以丢弃，但是否仍需要只能由你判断。",
            DeepSpaceAdviceKey.VideoFileTypeSummary => $"视频文件（{subject}）在此扫描根目录中占用了明显空间。",
            DeepSpaceAdviceKey.DiskImageFileTypeSummary => "磁盘镜像（.iso）在此扫描根目录中占用了明显空间。",
            DeepSpaceAdviceKey.InstallerFileTypeSummary => $"安装包文件（{subject}）在此扫描根目录中占用了明显空间。",
            DeepSpaceAdviceKey.ArchiveFileTypeSummary => $"压缩包文件（{subject}）在此扫描根目录中占用了明显空间。",
            DeepSpaceAdviceKey.LogFileTypeSummary => "日志文件（.log）在此扫描根目录中占用了明显空间。",
            DeepSpaceAdviceKey.TemporaryFileTypeSummary => $"临时文件特征的文件（{subject}）在此扫描根目录中占用了明显空间。",
            DeepSpaceAdviceKey.NoExtensionFileTypeSummary => "无扩展名文件在此扫描根目录中占用了明显空间。",
            DeepSpaceAdviceKey.GenericFileTypeSummary => $"{subject} 文件在此扫描根目录中占用了明显空间。",
            _ => item.Explanation
        };
    }

    public static string FormatSuggestedAction(Language language, DeepSpaceItem item)
    {
        if (language != Language.SimplifiedChinese)
        {
            return item.SuggestedAction;
        }

        return item.AdviceKey switch
        {
            DeepSpaceAdviceKey.NodeModules => "打开项目目录，确认 package manifest 存在且依赖可重新安装后，再手动删除 node_modules。",
            DeepSpaceAdviceKey.PythonVirtualEnvironment => "确认 Python 环境不再需要或可以重建后，再从项目目录中手动删除它。",
            DeepSpaceAdviceKey.TargetBuildOutput => "确认不需要保留构建产物后，优先使用项目工具清理，或手动删除此目录。",
            DeepSpaceAdviceKey.DotNetBuildOutput => "优先使用项目的 clean/build 清理命令。只有确认不需要生成输出后再手动删除。",
            DeepSpaceAdviceKey.GradleProjectCache => "优先使用 Gradle 清理命令；只有在项目关闭且缓存可重建时再手动删除。",
            DeepSpaceAdviceKey.FrontendFrameworkOutput => "确认项目没有正在运行后，优先使用框架清理命令；如果可以重建，再手动删除此目录。",
            DeepSpaceAdviceKey.FrontendBuildCache => "确认下一次构建可以重建缓存后，再手动删除。",
            DeepSpaceAdviceKey.ProjectLocalCache => "先打开项目并确认所属工具，再手动删除这个缓存目录。",
            DeepSpaceAdviceKey.PythonToolCache => "确认不再需要 Python 工具状态后，手动删除或让工具稍后重新生成。",
            DeepSpaceAdviceKey.TerraformWorkingDirectory => "不要在 Terraform 运行时删除。确认 provider/module 状态可以恢复后，再手动删除。",
            DeepSpaceAdviceKey.ProjectBuildOutput => "确认不需要保留打包产物后，优先使用项目工具清理，或手动移动/删除。",
            DeepSpaceAdviceKey.CoverageOutput => "如果不需要保存覆盖率报告，可以手动删除；通常可通过重新运行测试生成。",
            DeepSpaceAdviceKey.VendorDependencies => "确认依赖可以重新安装，且没有故意保留的 vendored 源码后，再手动删除。",
            DeepSpaceAdviceKey.GenericProjectDependency => "打开项目目录，确认它可以重建；如果不再需要本地构建或依赖状态，再手动删除。",
            DeepSpaceAdviceKey.LargeFolder => "打开目录，决定是否归档、移动到其他磁盘，或用所属应用/工具清理。",
            DeepSpaceAdviceKey.VideoFile => "手动查看视频内容，优先考虑移动到外置存储或归档盘，而不是直接删除。",
            DeepSpaceAdviceKey.LogFile => "先确认日志由哪个应用生成。如果旧日志不再用于排障，可用所属应用轮转日志或手动删除。",
            DeepSpaceAdviceKey.TemporaryFile => "先关闭相关应用，再确认文件已经过期，然后手动删除。",
            DeepSpaceAdviceKey.BackupFile => "确认已经有更新的备份副本后，再删除或移动此文件。",
            DeepSpaceAdviceKey.GenericLargeFile => "打开所在目录，手动决定保留、移动、归档或删除。",
            DeepSpaceAdviceKey.DiskImage => "如果不确定，先挂载或检查镜像内容；确认不再需要后再归档到其他位置或手动删除。",
            DeepSpaceAdviceKey.Installer => "确认不需要用于修复、回滚或离线安装，且可以重新下载后，再手动删除。",
            DeepSpaceAdviceKey.Archive => "删除前先检查压缩包内容。如果只是重复导出或旧包，可手动移动或删除。",
            DeepSpaceAdviceKey.GenericArchiveOrInstaller => "打开所在目录，确认应用或压缩包不再需要后，再手动删除。",
            DeepSpaceAdviceKey.VideoFileTypeSummary => "筛选到大文件并逐个查看视频；通常移动到外置存储比删除更安全。",
            DeepSpaceAdviceKey.DiskImageFileTypeSummary => "逐个复查磁盘镜像，只保留仍用于安装、恢复或归档的镜像。",
            DeepSpaceAdviceKey.InstallerFileTypeSummary => "逐个复查安装包，只删除可以重新下载或不再需要的安装包。",
            DeepSpaceAdviceKey.ArchiveFileTypeSummary => "删除压缩包前先检查内容，因为其中可能有唯一导出或项目快照。",
            DeepSpaceAdviceKey.LogFileTypeSummary => "先确认所属应用，再使用应用的日志轮转功能，或在不再需要时手动删除旧日志。",
            DeepSpaceAdviceKey.TemporaryFileTypeSummary => "关闭相关应用后，再手动复查这些看起来像临时文件的旧文件。",
            DeepSpaceAdviceKey.NoExtensionFileTypeSummary => "打开扫描根目录手动复查；无扩展名文件仍可能是重要应用或项目数据。",
            DeepSpaceAdviceKey.GenericFileTypeSummary => "打开扫描根目录，手动复查这种文件类型；ClearPilot 只报告合计大小。",
            _ => item.SuggestedAction
        };
    }
}
