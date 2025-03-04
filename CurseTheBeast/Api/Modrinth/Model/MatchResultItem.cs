using System.Text.Json.Serialization;

namespace CurseTheBeast.Api.Modrinth.Model;

public partial class MatchResultItem
{
    public FileInfo[] files { get; init; } = null!;

    public class FileInfo
    {
        public string url { get; init; } = null!;
        public FileHashes hashes { get; init; } = null!;

        public class FileHashes
        {
            public string sha1 { get; init; } = null!;
            public string sha512 { get; init; } = null!;
        }
    }


    [JsonSerializable(typeof(Dictionary<string, MatchResultItem>))]
    public partial class MatchResultDictContext : JsonSerializerContext
    {

    }
}
