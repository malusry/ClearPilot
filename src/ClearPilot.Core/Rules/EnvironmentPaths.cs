namespace ClearPilot.Core.Rules;

public sealed record EnvironmentPaths(
    string UserTemp,
    string LocalAppData,
    string UserProfile)
{
    public static EnvironmentPaths Current()
    {
        return new EnvironmentPaths(
            Path.GetTempPath(),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
    }
}
