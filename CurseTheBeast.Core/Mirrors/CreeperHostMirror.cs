namespace CurseTheBeast.Core.Mirrors;


public class CreeperHostMirror : HostReplacementMirror
{
    public static readonly CreeperHostMirror Instance = new();

    public override bool CN => false;

    public CreeperHostMirror() : base(
        new[]
        {
            "maven.minecraftforge.net",
            // 太垃圾了，不要替换mojang maven
        },
        new[]
        {
            "maven.creeperhost.net",
        })
    {

    }
}
