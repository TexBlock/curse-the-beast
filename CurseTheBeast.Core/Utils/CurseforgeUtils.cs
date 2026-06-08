namespace CurseTheBeast.Core.Utils;


public static class CurseforgeUtils
{
    public static string GetDownloadUrl(long fileId, string fileName)
        => $"https://edge.forgecdn.net/files/{fileId / 1000}/{fileId % 1000}/{Uri.EscapeDataString(fileName)}";
}
