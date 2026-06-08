using System;
using System.IO;

namespace CurseTheBeast.GUI.Models;

public sealed class AppSettings
{
    public string DownloadPath { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "CurseTheBeast.Downloads");

    public bool DownloadFullPack { get; set; } = true;
    public bool LoadAllOnStartup { get; set; } = true;
    public string ThemeMode { get; set; } = "System";
    public string WindowTransparencyMode { get; set; } = "Mica";
    
}
