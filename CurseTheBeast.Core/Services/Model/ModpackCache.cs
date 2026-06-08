using System.Text.Json.Serialization;

namespace CurseTheBeast.Core.Services.Model;


public partial class ModpackCache
{
    public Dictionary<int, Item> Items { get; set; } = new Dictionary<int, Item>();

    public class Item
    {
        public string Name { get; set; } = null!;
    }

    [JsonSerializable(typeof(ModpackCache))]
    public partial class ModpackCacheContext : JsonSerializerContext
    {

    }
}
