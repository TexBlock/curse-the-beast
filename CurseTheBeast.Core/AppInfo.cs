using System.Reflection;

namespace CurseTheBeast.Core;


public static class AppInfo
{
    public static readonly string Name;
    public static readonly string Version;
    public static readonly string Author;

    static AppInfo()
    {
        var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        Name = assembly.GetCustomAttribute<AssemblyTitleAttribute>()?.Title ?? "CurseTheBeast.Core";
        Version = assembly.GetName().Version?.ToString(3) ?? "0.0";
        Author = assembly.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company ?? "TomatoPuddin";
    }
}
