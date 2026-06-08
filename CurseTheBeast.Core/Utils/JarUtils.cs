using System.IO.Compression;
using System.Text;

namespace CurseTheBeast.Core.Utils;


public static class JarUtils
{
    // linux下也是CRLF
    const string LineEnd = "\r\n";
    const string EntryName = "META-INF/MANIFEST.MF";
    const int MaxLineSize = 72;

    public static async Task<Dictionary<string, string>> ReadJarManifestAsync(this ZipArchive archive)
    {
        var result = new Dictionary<string, string>();
        var entry = archive.GetEntry(EntryName);
        if (entry == null)
            return result;

        using var sr = new StreamReader(entry.Open(), encoding: MyEncoding.UTF8, leaveOpen: false);
        string? lastLine;
        while(true)
        {
             lastLine = await sr.ReadLineAsync();
            if (lastLine == null)
                return result;
            if (!string.IsNullOrWhiteSpace(lastLine))
                break;
        }

        while (true)
        {
            var splitIndex = lastLine.IndexOf(':');
            var key = lastLine.Substring(0, splitIndex);
            var value = new StringBuilder(lastLine.Substring(splitIndex + 2).TrimEnd('\r'));

            while (true)
            {
                var line = await sr.ReadLineAsync();
                if (line == null)
                {
                    result[key] = value.ToString();
                    return result;
                }
                if(string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }
                if (!line.StartsWith(' '))
                {
                    lastLine = line;
                    result[key] = value.ToString();
                    break;
                }
                value.Append(line[1..].TrimEnd('\r'));
            }
        }
    }

    public static async Task WriteJarManifestAsync(this ZipArchive archive, Dictionary<string, string> kv)
    {
        var entry = archive.CreateEntry(EntryName, CompressionLevel.Fastest);
        using var sw = new StreamWriter(entry.Open(), encoding: MyEncoding.UTF8, leaveOpen: false);
        foreach(var (k, v) in kv)
        {
            if(k.Length + 2 + v.Length <= MaxLineSize)
            {
                await sw.WriteAsync($"{k}: {v}{LineEnd}");
                continue;
            }

            await sw.WriteAsync(k);
            await sw.WriteAsync(": ");
            for (var i = 0; i < v.Length; )
            {
                if (i == 0)
                {
                    var len = MaxLineSize - k.Length - 2;
                    await sw.WriteAsync(v[..len]);
                    await sw.WriteAsync(LineEnd);
                    i += len;
                }
                else
                {
                    var len = Math.Min(MaxLineSize - 1, v.Length - i);
                    await sw.WriteAsync(' ');
                    await sw.WriteAsync(v.Substring(i, len));
                    await sw.WriteAsync(LineEnd);
                    i += len;
                }
            }
        }

        await sw.WriteAsync(LineEnd);
        return;
    }
}
