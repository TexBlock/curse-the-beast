using CurseTheBeast.Core.Api.Azul.Model;

namespace CurseTheBeast.Core.Api.Azul;


public class AzulApiClient : BaseApiClient
{
    public AzulApiClient()
    {

    }

    public Task<ZuluPackage[]> GetZuluPackageAsync(string javaVersion, string os, string arch, string archiveType, string pkgType, CancellationToken ct = default)
    { 
        return GetAsync<ZuluPackage[]>(new Uri($"https://api.azul.com/metadata/v1/zulu/packages/?" +
            $"java_version={javaVersion}&" +
            $"os={os}&" +
            $"arch={arch}&" +
            $"archive_type={archiveType}&" +
            $"java_package_type={pkgType}&" +
            $"javafx_bundled=false&" +
            $"release_status=ga&" +
            $"availability_types=CA&" +
            $"page=1&" +
            $"page_size=5"), ZuluPackage.ZuluPackageArrayContext.Default.ZuluPackageArray, ct);
    }
}
