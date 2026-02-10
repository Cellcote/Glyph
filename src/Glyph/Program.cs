using System.CommandLine;
using Glyph.Commands;
using Glyph.Services;
using Spectre.Console;

// Allow 'update' to run outside a git repository
var isUpdateCommand = args.Length > 0 && args[0] == "update";

if (!isUpdateCommand && !GitService.IsGitRepository())
{
    AnsiConsole.MarkupLine("[red]Error:[/] Not a git repository (or any parent up to mount point).");
    return 1;
}

// Start the update check in the background (non-blocking)
var updateCheckTask = UpdateChecker.CheckForUpdateAsync();

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
rootCommand.Subcommands.Add(UpdateCommand.Create());

// Default to tree view when no subcommand is given
rootCommand.SetAction(parseResult =>
{
    TreeCommand.Create().Parse(Array.Empty<string>()).Invoke();
});

var result = rootCommand.Parse(args).Invoke();

// Show update notification if available (don't delay if check hasn't finished)
if (updateCheckTask.IsCompleted)
{
    var updateMessage = await updateCheckTask;
    if (updateMessage != null)
        AnsiConsole.MarkupLine(updateMessage);
}

return result;
