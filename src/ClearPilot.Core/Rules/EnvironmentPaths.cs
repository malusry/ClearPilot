namespace ClearPilot.Core.Rules;

public sealed record EnvironmentPaths(
    string UserTemp,
    string LocalAppData,
    string UserProfile,
    string Windows = "",
    string ProgramData = "",
    string ProgramFiles = "",
    string ProgramFilesX86 = "")
{
    public static EnvironmentPaths Current()
    {
        return new EnvironmentPaths(
            Path.GetTempPath(),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86));
    }
}
