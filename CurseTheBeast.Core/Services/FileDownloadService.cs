using CurseTheBeast.Core.Download;
using CurseTheBeast.Core.Diagnostics;
using CurseTheBeast.Core.Storage;

namespace CurseTheBeast.Core.Services;

public static class FileDownloadService
{
    public static async Task DownloadAsync(string hint, IReadOnlyCollection<FileEntry> files, bool checkCache, CancellationToken ct = default)
    {
        if (files.Count == 0)
        {
            CoreLog.Verbose(nameof(FileDownloadService), $"Skip empty download request for \"{hint}\".");
            return;
        }

        if (checkCache && files.All(file => file.Validate()))
        {
            CoreLog.Info(nameof(FileDownloadService), $"Cache hit for \"{hint}\" ({files.Count} file(s)).");
            return;
        }

        CoreLog.Info(nameof(FileDownloadService), $"Downloading \"{hint}\" with {files.Count} file(s).");
        using var queue = new DownloadQueue();
        await queue.DownloadAsync(files);
        CoreLog.Info(nameof(FileDownloadService), $"Download finished for \"{hint}\".");
    }
}
