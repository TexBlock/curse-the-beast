using System.Text.Json.Serialization;

namespace CurseTheBeast.Core.Services.Model;

public partial class ModrinthCache
{
    public Dictionary<string, string?> Urls { get; set; } = [];


    [JsonSerializable(typeof(ModrinthCache))]
    public partial class ModrinthCacheContext : JsonSerializerContext
    {

    }
}
