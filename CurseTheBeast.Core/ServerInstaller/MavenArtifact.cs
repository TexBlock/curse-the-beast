namespace CurseTheBeast.Core.ServerInstaller;


public class MavenArtifact
{
    public string Id { get; }
    public string Name { get; }
    public string UrlPath { get; }
    public string FilePath { get; }
    public string FileName { get; }
    public string Version { get; }
    public string Format { get; }
    public string Namespace { get; }
    public string? Constraint { get; }

    public MavenArtifact(string id)
    {
        Id = id;

        if (id.Contains('@'))
        {
            var index = id.IndexOf('@');
            Format = id.Substring(index + 1);
            id = id.Remove(index);
        }
        else
        {
            Format = "jar";
        }

        var p = id.Split(':');
        Namespace = p[0];
        Name = p[1];
        Version = p[2];
        if (p.Length > 3)
        {
            Constraint = p[3];
            FileName = $"{Name}-{Version}-{Constraint}.{Format}";
        }
        else
        {
            FileName = $"{Name}-{Version}.{Format}";
        }

        var slices = Namespace.Split('.').Concat(new[] { Name, Version, FileName }).ToArray();
        UrlPath = string.Join('/', slices);
        FilePath = Path.Combine(slices);
    }
}
