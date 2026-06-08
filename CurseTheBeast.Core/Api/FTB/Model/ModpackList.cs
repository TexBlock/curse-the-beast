using System.Text.Json.Serialization;

namespace CurseTheBeast.Core.Api.FTB.Model;


public partial class ModpackList : FTBRsp
{
    public int[] packs { get; set; } = null!;
    public int total { get; set; }
    public long refreshed { get; set; }


    [JsonSerializable(typeof(ModpackList))]
    public partial class ModpackListContext : JsonSerializerContext
    {

    }
}
