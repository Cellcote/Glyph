using System.CommandLine;
using Glyph.Commands;
using Glyph.Services;
using Spectre.Console;

if (!GitService.IsGitRepository())
{
    AnsiConsole.MarkupLine("[red]Error:[/] Not a git repository (or any parent up to mount point).");
    return 1;
}

var rootCommand = new RootCommand("Glyph - A Git TUI for trunk-based development workflows");
rootCommand.Subcommands.Add(TreeCommand.Create());
rootCommand.Subcommands.Add(ParentCommand.Create());
rootCommand.Subcommands.Add(RebaseCommand.Create());
rootCommand.Subcommands.Add(PrCommand.Create());
rootCommand.Subcommands.Add(SyncCommand.Create());
rootCommand.Subcommands.Add(StackCommand.Create());
rootCommand.Subcommands.Add(AddCommand.Create());
rootCommand.Subcommands.Add(CommitCommand.Create());
rootCommand.Subcommands.Add(EditCommand.Create());
rootCommand.Subcommands.Add(ShipCommand.Create());

// Default to tree view when no subcommand is given
rootCommand.SetAction(parseResult =>
{
    TreeCommand.Create().Parse(Array.Empty<string>()).Invoke();
});

return rootCommand.Parse(args).Invoke();
