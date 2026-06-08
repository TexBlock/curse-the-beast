using System.Text.Json.Serialization;

namespace CurseTheBeast.Core.Services.Model;

public partial class CurseforgeCache
{
    public Dictionary<string, Item?> Items { get; set; } = [];

    public class Item
    {
        public long ProjectId { get; set; }
        public long FileId { get; set; }
    }

    [JsonSerializable(typeof(CurseforgeCache))]
    public partial class CurseforgeCacheContext : JsonSerializerContext
    {

    }
}
