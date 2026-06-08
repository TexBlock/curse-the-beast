using CurseTheBeast.Core.Storage;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;

namespace CurseTheBeast.Core.Utils;


public static partial class ZipExtensions
{
    const int UnixExecutableAttr = -2115174400;

    public static async Task<ZipArchiveEntry> WriteFileAsync(this ZipArchive archive, string? entryPrefix, FileEntry file, CancellationToken ct)
    {
        if (file.ArchiveEntryName == null)
            throw new Exception($"文件\"{file.DisplayName ?? file.LocalPath}\"未设置EntryName");

        ZipArchiveEntry entry;
        if (string.IsNullOrWhiteSpace(entryPrefix))
            entry = await archive.WriteLocalFileAsync(file.ArchiveEntryName!, file.LocalPath, ct);
        else
            entry = await archive.WriteLocalFileAsync($"{entryPrefix}/{file.ArchiveEntryName!}", file.LocalPath, ct);
        if (file.UnixExecutable)
            entry.ExternalAttributes = UnixExecutableAttr;
        return entry;
    }

    public static async Task<ZipArchiveEntry> WriteLocalFileAsync(this ZipArchive archive, string entryName, string filePath, CancellationToken ct)
    {
        var entry = archive.CreateEntry(entryName, getCompressionLevel(entryName));
        await using var entryStream = entry.Open();
        await using var assetStream = File.OpenRead(filePath);
        await assetStream.CopyToAsync(entryStream, ct);
        return entry;
    }

    public static async Task<ZipArchiveEntry> WriteTextFileAsync(this ZipArchive archive, string entryName, string content, CancellationToken ct)
    {
        var entry = archive.CreateEntry(entryName, getCompressionLevel(entryName));
        await using var entryStream = entry.Open();
        await using var assetStream = new StreamWriter(entryStream, leaveOpen: true, encoding: MyEncoding.GBK);
        await assetStream.WriteAsync(content.AsMemory(), ct);
        return entry;
    }

    public static async Task<ZipArchiveEntry> WriteJsonFileAsync(this ZipArchive archive, string entryName, JsonNode obj, CancellationToken ct)
    {
        var entry = archive.CreateEntry(entryName, getCompressionLevel(entryName));
        await using var entryStream = entry.Open();
        await JsonSerializer.SerializeAsync(entryStream, obj, JsonNodeContext.Default.JsonNode, ct);
        return entry;
    }

    public static async Task<ZipArchiveEntry> WriteJsonFileAsync<T>(this ZipArchive archive, string entryName, T model, JsonTypeInfo<T> typeInfo, CancellationToken ct)
    {
        var entry = archive.CreateEntry(entryName, getCompressionLevel(entryName));
        await using var entryStream = entry.Open();
        await JsonSerializer.SerializeAsync(entryStream, model, typeInfo, ct);
        return entry;
    }

    static CompressionLevel getCompressionLevel(string entryName)
    {
        /*
        if (entryName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ||
                 entryName.EndsWith(".jar", StringComparison.OrdinalIgnoreCase))
            return CompressionLevel.NoCompression;
        else
            return CompressionLevel.SmallestSize;
        */
        return CompressionLevel.Fastest;
    }
}
