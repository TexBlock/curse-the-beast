namespace CurseTheBeast.Core.Packs;

public interface IPackProcessor : IDisposable
{
    string DefaultFileName { get; }
    Task DownloadAsync(CancellationToken ct = default);
    Task ProcessAsync(CancellationToken ct = default);
    Task PackAsync(Stream stream, string dstHint, CancellationToken ct = default);
}
