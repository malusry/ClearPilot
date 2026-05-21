using System.Runtime.InteropServices;

namespace ClearPilot.Core.Analysis;

public sealed class UserSpecialFolderProvider : IDeepSpaceSpecialFolderProvider
{
    private static readonly Guid DownloadsKnownFolderId = new("374DE290-123F-4565-9164-39C4925E467B");

    private readonly Func<string?> knownFolderDownloadsPathResolver;
    private readonly Func<string?> userProfilePathResolver;
    private readonly Func<string, bool> directoryExists;

    public UserSpecialFolderProvider(
        Func<string?>? knownFolderDownloadsPathResolver = null,
        Func<string?>? userProfilePathResolver = null,
        Func<string, bool>? directoryExists = null)
    {
        this.knownFolderDownloadsPathResolver = knownFolderDownloadsPathResolver ?? ResolveKnownFolderDownloadsPath;
        this.userProfilePathResolver = userProfilePathResolver ?? (() => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        this.directoryExists = directoryExists ?? Directory.Exists;
    }

    public string? TryGetDownloadsPath()
    {
        if (TryNormalizeUsablePath(knownFolderDownloadsPathResolver(), out var knownFolderDownloads))
        {
            return knownFolderDownloads;
        }

        var userProfile = userProfilePathResolver();
        if (string.IsNullOrWhiteSpace(userProfile))
        {
            return null;
        }

        var fallback = Path.Combine(userProfile, "Downloads");
        return TryNormalizeUsablePath(fallback, out var fallbackDownloads) ? fallbackDownloads : null;
    }

    private bool TryNormalizeUsablePath(string? path, out string normalizedPath)
    {
        normalizedPath = string.Empty;
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        try
        {
            var fullPath = Path.GetFullPath(path)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            if (!directoryExists(fullPath))
            {
                return false;
            }

            normalizedPath = fullPath;
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
        catch (PathTooLongException)
        {
            return false;
        }
    }

    private static string? ResolveKnownFolderDownloadsPath()
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        IntPtr pathPtr = IntPtr.Zero;
        try
        {
            var hr = SHGetKnownFolderPath(DownloadsKnownFolderId, 0, IntPtr.Zero, out pathPtr);
            if (hr != 0 || pathPtr == IntPtr.Zero)
            {
                return null;
            }

            return Marshal.PtrToStringUni(pathPtr);
        }
        catch (DllNotFoundException)
        {
            return null;
        }
        catch (EntryPointNotFoundException)
        {
            return null;
        }
        finally
        {
            if (pathPtr != IntPtr.Zero)
            {
                Marshal.FreeCoTaskMem(pathPtr);
            }
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHGetKnownFolderPath(
        [MarshalAs(UnmanagedType.LPStruct)] Guid rfid,
        uint dwFlags,
        IntPtr hToken,
        out IntPtr ppszPath);
}

