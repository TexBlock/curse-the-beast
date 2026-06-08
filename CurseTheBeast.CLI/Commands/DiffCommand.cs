using CurseTheBeast.CLI.Commands.Options;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace CurseTheBeast.CLI.Commands;


public class DiffCommand : AsyncCommand<DiffCommand.Options>
{
    public class Options : DownloadOptions
    {
        [Description("整合包ID")]
        [CommandArgument(0, "<PackId>")]
        public int PackId { get; set; }

        [Description("新版本ID")]
        [CommandArgument(1, "<VersionId>")]
        public int NewVersionId { get; set; }

        [Description("旧版本ID")]
        [CommandArgument(1, "<VersionId>")]
        public int OldVersionId { get; set; }
    }


    public override Task<int> ExecuteAsync(CommandContext context, Options options)
    {
        throw new NotImplementedException();
    }
}