namespace CurseTheBeast.Core.Utils;


public class DirectoryUtils
{
    public static void SetupOutputDirectory(string path, bool isDefaultCommand)
    {
        path = Path.GetFullPath(path);
        if (File.Exists(path))
        {
            ensureAccess(Path.GetDirectoryName(path)!, isDefaultCommand);
        }
        else if (Directory.Exists(path))
        {
            ensureAccess(path!, isDefaultCommand);
        }
        else if (path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            var dir = Path.GetDirectoryName(path)!;
            ensureAccess(dir, isDefaultCommand);
        }
        else
        {
            ensureAccess(path, isDefaultCommand);
        }
    }

    static void ensureAccess(string dirPath, bool isDefaultCommand)
    {
        var filePath = Path.Combine(dirPath, Math.Abs(Random.Shared.NextInt64()).ToString());
        try
        {
            if (!Directory.Exists(dirPath))
                Directory.CreateDirectory(dirPath);
            File.OpenHandle(filePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, FileOptions.DeleteOnClose).Dispose();
        }
        catch (Exception)
        {
            if (NativeUtils.IsRunningByDoubleClick.Value && isDefaultCommand)
                throw new Exception("当前目录无写入权限，请右键以管理员身份运行；或把程序移动到其他目录再试。");
            else if (isDefaultCommand)
                throw new Exception("当前目录无写入权限，请把程序移动到其他目录再试。");
            else if (Environment.CurrentDirectory == dirPath)
                throw new Exception($"当前目录 {dirPath} 无写入权限，请通过参数指定其它输出目录。");
            else
                throw new Exception($"输出目录 {dirPath} 无写入权限，请指定其它目录。");
        }
    }
}
