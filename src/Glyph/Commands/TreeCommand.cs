using System.CommandLine;
using Glyph.Services;
using Spectre.Console;

namespace Glyph.Commands;

public static class TreeCommand
{
    public static Command Create()
    {
        var command = new Command("tree") { Description = "Show branch tree with parent relationships" };

        command.SetAction(parseResult =>
        {
            using var git = new GitService();
            var branches = git.GetBranches();

            if (branches.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No branches found.[/]");
                return;
            }

            var defaultParent = git.GetParentBranch("__default__");
            TreeRenderer.Render(branches, defaultParent);
        });

        return command;
    }
}
