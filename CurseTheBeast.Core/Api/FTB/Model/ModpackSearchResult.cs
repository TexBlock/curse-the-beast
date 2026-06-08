using System.Text.Json.Serialization;

namespace CurseTheBeast.Core.Api.FTB.Model;


public partial class ModpackSearchResult : FTBRsp
{
    public Pack[] packs { get; init; } = null!;
    public int total { get; init; }
    public int limit { get; init; }
    public int refreshed { get; init; }


    public class Pack
    {
        public string platform { get; init; } = null!;
        public string name { get; init; } = null!;
        public Art[] art { get; init; } = null!;
        public Author[] authors { get; init; } = null!;
        public Tag[] tags { get; init; } = null!;
        public string synopsis { get; init; } = null!;
        public int id { get; init; }
        public int updated { get; init; }
        [JsonPropertyName("private")]
        public bool private_ { get; init; }


        public class Art
        {
            public int width { get; init; }
            public int height { get; init; }
            public bool compressed { get; init; }
            public string url { get; init; } = null!;
            public int size { get; init; }
            public int id { get; init; }
            public string type { get; init; } = null!;
        }


        public class Author
        {
            public string website { get; init; } = null!;
            public int id { get; init; }
            public string name { get; init; } = null!;
            public string type { get; init; } = null!;
            public int updated { get; init; }
        }


        public class Tag
        {
            public int id { get; init; }
            public string name { get; init; } = null!;
        }
    }

    [JsonSerializable(typeof(ModpackSearchResult))]
    public partial class ModpackSearchResultContext : JsonSerializerContext
    {

    }
}
