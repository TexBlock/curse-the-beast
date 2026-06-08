using CurseTheBeast.Core.Api.Curseforge.Model;
using System.Text.Json.Serialization;

namespace CurseTheBeast.Core.Api.Curseforge;

public partial class Contexts
{
    [JsonSerializable(typeof(GenericRsp<ModFile[]>))]
    public partial class ModFileArrayContext : JsonSerializerContext
    {

    }

    [JsonSerializable(typeof(GenericRsp<MatchResult>))]
    public partial class MatchResultContext : JsonSerializerContext
    {

    }
}
