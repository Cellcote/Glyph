using System.CommandLine;
using Glyph.Services;
using Spectre.Console;

namespace Glyph.Commands;

public static class StackCommand
{
    public static Command Create()
    {
        var command = new Command("stack") { Description = "Show the branch stack from current branch to trunk" };

        command.SetAction(parseResult =>
        {
            using var git = new GitService();
            var current = git.CurrentBranchName;

            var stack = new List<(string Name, int Ahead, int Behind)>();
            var visited = new HashSet<string>();
            var branch = current;

            while (!visited.Contains(branch))
            {
                visited.Add(branch);
                var parent = git.GetParentBranch(branch);
                var (ahead, behind) = git.GetDivergence(branch, parent);
                stack.Add((branch, ahead, behind));

                if (parent == branch)
                    break;
                branch = parent;
            }

            stack.Reverse();

            var table = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn("Branch")
                .AddColumn("Ahead")
                .AddColumn("Behind");

            foreach (var (name, ahead, behind) in stack)
            {
                var isCurrent = name == current;
                var nameCol = isCurrent ? $"[bold green]* {name}[/]" : $"  {name}";
                var aheadCol = ahead > 0 ? $"[green]+{ahead}[/]" : "[dim]0[/]";
                var behindCol = behind > 0 ? $"[red]-{behind}[/]" : "[dim]0[/]";
                table.AddRow(nameCol, aheadCol, behindCol);
            }

            AnsiConsole.Write(table);
        });

        return command;
    }
}
