using System.Text.Json.Serialization;

namespace CurseTheBeast.Core.Api.Fabric.Model;


public partial class ServerManifest
{
    public string mainClass { get; init; } = null!;
    public Library[] libraries { get; init; } = null!;


    public class Library
    {
        public string name { get; init; } = null!;
        public string url { get; init; } = null!;
    }

    [JsonSerializable(typeof(ServerManifest))]
    public partial class ServerManifestContext : JsonSerializerContext
    {

    }
}
