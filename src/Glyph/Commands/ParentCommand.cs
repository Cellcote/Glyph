using System.CommandLine;
using Glyph.Services;
using Spectre.Console;

namespace Glyph.Commands;

public static class ParentCommand
{
    public static Command Create()
    {
        var branchArg = new Argument<string?>("branch")
        {
            Description = "The parent branch to set. Omit to show current parent.",
            DefaultValueFactory = _ => null
        };

        var command = new Command("parent") { Description = "Get or set the parent branch for the current branch" };
        command.Arguments.Add(branchArg);

        command.SetAction(parseResult =>
        {
            var branch = parseResult.GetValue(branchArg);
            using var git = new GitService();
            var current = git.CurrentBranchName;

            if (branch == null)
            {
                var parent = git.GetParentBranch(current);
                AnsiConsole.MarkupLine($"Parent of [green]{current}[/]: [blue]{parent}[/]");
            }
            else
            {
                git.SetParentBranch(current, branch);
                AnsiConsole.MarkupLine($"Set parent of [green]{current}[/] to [blue]{branch}[/]");
            }
        });

        return command;
    }
}
