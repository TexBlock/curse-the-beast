using CurseTheBeast.CLI.Commands.Options;
using CurseTheBeast.Core.Services;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace CurseTheBeast.CLI.Commands;


[Description("查看整合包详情")]
public class InspectCommand : AsyncCommand<InspectCommand.Options>
{
    public class Options : HttpOptions
    {
        [Description("整合包ID")]
        [CommandArgument(0, "<PackId>")]
        public int PackId { get; set; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Options options)
    {
        HttpConfigService.SetupHttp(null, null, options.NoProxy, options.Proxy, options.UserAgent);
        using var ftb = new FTBService();
        var info = await ftb.GetModpackInfoAsync(options.PackId);

        var grid = new Grid();
        grid.AddColumn();
        grid.AddColumn();
        grid.AddRow(new Text("Name:"), new Text(info.name));
        grid.AddRow(new Text("Desc:"), new Text(info.synopsis));
        grid.AddRow(new Text("Tags:"), new Text(string.Join(", ", info.tags.Select(t => t.name))));
        grid.AddRow(new Text("Authors:"), new Text(string.Join(", ", info.authors.Select(t => t.name))));
        grid.AddRow(new Text("Link:"), new Text($"https://www.feed-the-beast.com/modpacks/{info.id}"));
        AnsiConsole.WriteLine();
        AnsiConsole.Write(grid);

        var table = new Table();
        table.Title = new ("Versions");
        table.AddColumn(new TableColumn("Id"));
        table.AddColumn(new TableColumn("Name"));
        table.AddColumn(new TableColumn("Type"));
        table.AddColumn(new TableColumn("LastUpdate"));
        foreach (var version in info.versions.OrderByDescending(v => v.updated))
        {
            table.AddRow(
                new Text(version.id.ToString()), 
                new Text(version.name.ToString()), 
                new Text(version.type.ToLower()), 
                new Text(DateTimeOffset.FromUnixTimeSeconds(version.updated).ToString("yyyy/MM/dd")));
        }
        AnsiConsole.WriteLine();
        AnsiConsole.Write(table);

        AnsiConsole.WriteLine();
        return 0;
    }
}
