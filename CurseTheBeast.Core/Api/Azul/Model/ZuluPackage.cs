using System.Text.Json.Serialization;

namespace CurseTheBeast.Core.Api.Azul.Model; 


public partial class ZuluPackage
{
    /// <summary>
    /// 又臭又长的文件名.zip
    /// </summary>
    public string name { get; set; } = null!;
    public int[] java_version { get; set; } = null!;
    public string download_url { get; set; } = null!;

    [JsonSerializable(typeof(ZuluPackage[]))]
    public partial class ZuluPackageArrayContext : JsonSerializerContext
    {

    }
}