namespace CurseTheBeast.Core.Mirrors;


public class BmclMirror : HostReplacementMirror
{
    public static readonly BmclMirror Instance = new();

    public BmclMirror() : base(new Dictionary<IReadOnlyList<string>, string>()
    {
        [new []
        {
            "launcher.mojang.com",
            "launchermeta.mojang.com",
            "piston-meta.mojang.com",
            "piston-data.mojang.com",
            "files.minecraftforge.net",
        }] = "bmclapi2.bangbang93.com",
        [new[]
        {
            "libraries.minecraft.net",
            "maven.minecraftforge.net",
            "maven.fabricmc.net",
            "maven.neoforged.net",
        }] = "bmclapi2.bangbang93.com/maven",
        /* 缺的太多
        [new[]
        {
            "meta.fabricmc.net",
        }] = "bmclapi2.bangbang93.com/fabric-meta",
        */
    })
    {

    }
}
