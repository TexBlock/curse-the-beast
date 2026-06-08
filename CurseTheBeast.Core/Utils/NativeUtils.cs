using System.Runtime.InteropServices;

namespace CurseTheBeast.Core.Utils;


public static unsafe partial class NativeUtils
{
    public static readonly Lazy<bool> IsRunningByDoubleClick = new (isRunningByDoubleClick, true);

    // copied from https://github.com/Mrs4s/go-cqhttp/blob/2af55d6a67ae7c45a22095e8c7e56d31e68e3fe8/global/terminal/double_click_windows.go
    static bool isRunningByDoubleClick()
    {
        if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            return false;

        int* processIds = stackalloc int[2];
        var count = GetConsoleProcessList(processIds, 2);

        return count <= 1;
    }

    [LibraryImport("kernel32.dll")]
    private static partial int GetConsoleProcessList(int* processIds, int maxCount);
}
