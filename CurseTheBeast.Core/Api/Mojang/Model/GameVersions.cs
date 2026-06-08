using System.Text.Json.Serialization;

namespace CurseTheBeast.Core.Api.Mojang.Model;


public partial class GameVersions
{
    public Version[] versions { get; init; } = null!;

    public class Version
    {
        public string id { get; init; } = null!;
        public string url { get; init; } = null!;
    }

    [JsonSerializable(typeof(GameVersions))]
    public partial class GameVersionsContext : JsonSerializerContext
    {

    }
}
