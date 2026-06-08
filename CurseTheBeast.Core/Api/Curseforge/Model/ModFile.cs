namespace CurseTheBeast.Core.Api.Curseforge.Model;


public class ModFile
{
    public long id { get; init; }
    public long gameId { get; init; }
    public long modId { get; init; }
    public string fileName { get; init; } = null!;
    public Hash[] hashes { get; init; } = null!;
    public int fileLength { get; init; }
    public string? downloadUrl { get; init; } = null!;

    public class Hash
    {
        public string value { get; init; } = null!;
        public int algo { get; init; }
    }
}