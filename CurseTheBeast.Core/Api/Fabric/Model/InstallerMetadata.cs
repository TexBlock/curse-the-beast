using CurseTheBeast.Core.Api.Curseforge.Model;
using System.Text.Json.Serialization;

namespace CurseTheBeast.Core.Api.Fabric.Model;


public partial class InstallerMetadata
{
    public string url { get; init; } = null!;
    public string version { get; init; } = null!;
    public bool stable { get; init; }


    [JsonSerializable(typeof(InstallerMetadata[]))]
    public partial class InstallerMetadataArrayContext : JsonSerializerContext
    {

    }
}
