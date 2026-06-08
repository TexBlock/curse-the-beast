using CurseTheBeast.Core.Storage;

namespace CurseTheBeast.Core.ServerInstaller;


public class MavenFileEntry : FileEntry
{
    public MavenArtifact Artifact { get; }

    public MavenFileEntry(string artifactid)
        : this(new MavenArtifact(artifactid))
    {

    }

    public MavenFileEntry(MavenArtifact artifact)
        : base(RepoType.MavenArtifact, artifact.FilePath)
    {
        Artifact = artifact;
    }

    public MavenFileEntry WithMavenRepo(string repoUrl)
    {
        SetDownloadable(Artifact.FileName, $"{repoUrl.TrimEnd('/')}/{Artifact.UrlPath}");
        return this;
    }

    public MavenFileEntry WithMavenUrl(string url)
    {
        SetDownloadable(Artifact.FileName, url);
        return this;
    }

    public MavenFileEntry WithMavenBaseArchiveEntryName(string baseEntryName = "libraries") 
    {
        WithArchiveEntryName(baseEntryName, Artifact.UrlPath);
        return this;
    }
}
