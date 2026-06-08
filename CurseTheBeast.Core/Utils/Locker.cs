using Microsoft.Win32.SafeHandles;
using System.Diagnostics.CodeAnalysis;

namespace CurseTheBeast.Core.Utils;


public class Locker : IDisposable
{
    public bool IsNew { get; }

    private readonly SafeFileHandle _handle;

    private Locker(SafeFileHandle handle, bool isNew)
    {
        _handle = handle;
        IsNew = isNew;
    }

    public static bool TryAcquire(string path, [NotNullWhen(true)] out Locker? locker)
    {
        try
        {
            locker = new Locker(File.OpenHandle(path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, FileOptions.DeleteOnClose), true);
            return true;
        }
        catch (IOException)
        {
            locker = null;
            return false;
        }
    }

    public static async Task<Locker> AcquireOrWaitAsync(string path, CancellationToken ct)
    {
        SafeFileHandle handle;
        bool isNew = true;
        while (true)
        {
            try
            {
                handle = File.OpenHandle(Path.TrimEndingDirectorySeparator(path) + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, FileOptions.DeleteOnClose);
                break;
            }
            catch (IOException)
            {
                isNew = false;
                await Task.Delay(TimeSpan.FromSeconds(3), ct);
            }
        }
        return new Locker(handle, isNew);
    }

    public void Dispose()
    {
        _handle.Dispose();
    }
}
