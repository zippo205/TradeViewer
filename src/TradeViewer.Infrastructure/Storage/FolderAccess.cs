using System.Runtime.InteropServices;

namespace TradeViewer.Infrastructure.Storage;

/// <summary>
/// Whether the process may write into a folder, without writing anything there: on Linux (the application's
/// platform, decision 0010) the system answers from the folder's mode, its owner and the mount (a read-only volume
/// answers no). TradeViewer writes in the scenario folder only in expert/ and only when the user changed something,
/// so the start-up check of that folder must not leave even a short-lived test file.
/// </summary>
internal static class FolderAccess
{
    private const int WriteAccess = 2; // W_OK

    public static bool CanWriteWithoutWriting(string folder)
    {
        if (OperatingSystem.IsWindows())
        {
            return !new DirectoryInfo(folder).Attributes.HasFlag(FileAttributes.ReadOnly); // development only
        }

        return access(folder, WriteAccess) == 0;
    }

#pragma warning disable SYSLIB1054 // a plain DllImport keeps the project free of unsafe code
    [DllImport("libc", SetLastError = true)]
    private static extern int access(string path, int mode);
#pragma warning restore SYSLIB1054
}
