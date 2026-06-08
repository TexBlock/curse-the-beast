using CurseTheBeast.Core.Api.Modrinth.Model;
using System.Text.Json.Nodes;

namespace CurseTheBeast.Core.Api.Modrinth;


public class ModrinthApiClient : BaseApiClient
{
    protected override void OnConfigureHttpClient(HttpClient client)
    {
        client.BaseAddress = new Uri("https://api.modrinth.com/");
    }

    public async ValueTask<Dictionary<string, MatchResultItem>> MatchFilesAsync(IEnumerable<string> sha1List, CancellationToken ct)
    {
        return await PostJsonAsync<Dictionary<string, MatchResultItem>>(new Uri($"v2/version_files", UriKind.Relative), new JsonObject
        {
            ["hashes"] = new JsonArray(sha1List.Select(f => JsonValue.Create(f)).ToArray()),
            ["algorithm"] = "sha1"
        }, MatchResultItem.MatchResultDictContext.Default.DictionaryStringMatchResultItem, ct);
    }
}
