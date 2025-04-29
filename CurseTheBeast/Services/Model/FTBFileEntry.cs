using CurseTheBeast.Api.FTB.Model;
using CurseTheBeast.Storage;

namespace CurseTheBeast.Services.Model;


public class FTBFileEntry : FileEntry
{
    public FileSide Side { get; }
    public bool Optional { get; }
    [Obsolete]
    public CurseforgeInfo? Curseforge { get; private set; }
    public bool IsMod { get; }
    public long CFMurmur { get; }
    public string Sha512 { get; }

    public FTBFileEntry(ModpackManifest.File file)
        : base(RepoType.AssetV2, getAssetCachePath(file.hashes.sha1))
    {
        CFMurmur = file.hashes.cfMurmur;
        IsMod = file.type.Equals("mod", StringComparison.OrdinalIgnoreCase);
        Sha512 = file.hashes.sha512;
        Side = file switch
        {
            { serveronly: true } => FileSide.Server,
            { clientonly: true } => FileSide.Client,
            _ => FileSide.Both,
        };
        Optional = file.optional;

        WithSha1(file.hashes.sha1);
        WithSize(file.size);
        WithArchiveEntryName(file.path, file.name);

        if (ArchiveEntryName!.StartsWith("mods/", StringComparison.OrdinalIgnoreCase) 
            && ArchiveEntryName.EndsWith(".jar.disabled", StringComparison.OrdinalIgnoreCase))
            ArchiveEntryName = ArchiveEntryName.Remove(ArchiveEntryName.Length - 9);

        SetDownloadable(file.name, [file.url, ..file.mirrors]);

    }

    [Obsolete]
    public FTBFileEntry WithCurseforgeInfo(long projectId, long fileId)
    {
        Curseforge = new CurseforgeInfo()
        {
            ProjectId = projectId,
            FileId = fileId
        };
        return this;
    }

    static string[] getAssetCachePath(string sha1)
    {
        sha1 = sha1.ToLower();
        return [sha1[..2], sha1[2..]];
    }

    public class CurseforgeInfo
    {
        public long ProjectId { get; init; }
        public long FileId { get; init; }
    }

    [Flags]
    public enum FileSide
    {
        Client = 1,
        Server = 2,
        Both = Client | Server
    }
}
