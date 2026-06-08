namespace CurseTheBeast.Core.Mirrors;


public class Lss233Mirror : HostReplacementMirror
{
    public static readonly Lss233Mirror Instance = new();

    public Lss233Mirror() : base(
        new[] 
        {
            "maven.minecraftforge.net",
            "libraries.minecraft.net",
         // fabric官方源似乎比它快一点
         // "maven.fabricmc.net"
        }, 
        new[]
        {
            // "lss233.littleservice.cn/repositories/minecraft",
            "crystal.app.lss233.com/repositories/minecraft",
            "maven.fastmirror.net/repositories/minecraft"
        })
    {

    }
}
