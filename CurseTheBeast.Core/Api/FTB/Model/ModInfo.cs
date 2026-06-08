using System.Text.Json.Serialization;

namespace CurseTheBeast.Core.Api.FTB.Model;


public partial class ModInfo : FTBRsp
{
    public Version[] versions { get; set; } = null!;
    public int id { get; set; }
    public string name { get; set; } = null!;

    public class Link
    {
        public string link { get; set; } = null!;
    }

    public class Version
    {
        public string url { get; set; } = null!;
        public string sha1 { get; set; } = null!;
        public int size { get; set; }
        public long id { get; set; }
    }

    [JsonSerializable(typeof(ModInfo))]
    public partial class ModInfoContext : JsonSerializerContext
    {

    }
}
