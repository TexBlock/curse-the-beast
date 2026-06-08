using System.Text.Json.Serialization;

namespace CurseTheBeast.Core.Api.FTB.Model;


public partial class ModpackInfo : FTBRsp
{
    public string synopsis { get; init; } = null!;
    public string description { get; init; } = null!;
    public Art[] art { get; init; } = null!;
    public Link[] links { get; init; } = null!;
    public Author[] authors { get; init; } = null!;
    public Version[] versions { get; init; } = null!;
    public int installs { get; init; }
    public int plays { get; init; }
    public Tag[] tags { get; init; } = null!;
    public bool featured { get; init; }
    public int refreshed { get; init; }
    public string notification { get; init; } = null!;
    public Rating rating { get; init; } = null!;
    public int released { get; init; }
    public int plays_14d { get; init; }
    public int id { get; init; }
    public string name { get; init; } = null!;
    public string type { get; init; } = null!;
    public int updated { get; init; }
    [JsonPropertyName("private")]
    public bool private_ { get; init; }


    public class Link
    {
        public int id { get; init; }
        public string name { get; init; } = null!;
        public string link { get; init; } = null!;
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


    public class Rating
    {
        public int id { get; init; }
        public bool configured { get; init; }
        public bool verified { get; init; }
        public int age { get; init; }
        public bool gambling { get; init; }
        public bool frightening { get; init; }
        public bool alcoholdrugs { get; init; }
        public bool nuditysexual { get; init; }
        public bool sterotypeshate { get; init; }
        public bool language { get; init; }
        public bool violence { get; init; }
    }


    public class Art
    {
        public int width { get; init; }
        public int height { get; init; }
        public bool compressed { get; init; }
        public string url { get; init; } = null!;
        /*
        public object[] mirrors { get; init; } = null!;
        */
        public string sha1 { get; init; } = null!;
        public int size { get; init; }
        public int id { get; init; }
        /// <summary>
        /// square
        /// </summary>
        public string type { get; init; } = null!;
        public int updated { get; init; }
    }


    public class Version
    {
        public Specs specs { get; init; } = null!;
        public Target[] targets { get; init; } = null!;
        public int id { get; init; }
        public string name { get; init; } = null!;
        public string type { get; init; } = null!;
        public int updated { get; init; }
        [JsonPropertyName("private")]
        public bool private_ { get; init; }


        public class Target
        {
            public string version { get; init; } = null!;
            public int id { get; init; }
            public string name { get; init; } = null!;
            public string type { get; init; } = null!;
            public int updated { get; init; }
        }


        public class Specs
        {
            public int id { get; init; }
            public int minimum { get; init; }
            public int recommended { get; init; }
        }
    }


    public class Tag
    {
        public int id { get; set; }
        public string name { get; set; } = null!;
    }


    [JsonSerializable(typeof(ModpackInfo))]
    public partial class ModpackInfoContext : JsonSerializerContext
    {

    }
}