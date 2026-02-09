using System.CommandLine;
using Glyph.Services;
using Spectre.Console;

namespace Glyph.Commands;

public static class RebaseCommand
{
    public static Command Create()
    {
        var command = new Command("rebase") { Description = "Rebase current branch onto its parent branch" };

        command.SetAction(async (ParseResult parseResult, CancellationToken ct) =>
        {
            using var git = new GitService();
            var current = git.CurrentBranchName;
            var parent = git.GetParentBranch(current);

            AnsiConsole.MarkupLine($"Rebasing [green]{current}[/] onto [blue]{parent}[/]...");

            // Fetch first
            var (fetchExit, _, fetchErr) = await ProcessRunner.RunAsync("git", $"fetch origin {parent}");
            if (fetchExit != 0)
            {
                AnsiConsole.MarkupLine($"[yellow]Warning: fetch failed ({Markup.Escape(fetchErr)}), continuing with local state...[/]");
            }

            // Rebase
            var (exitCode, output, error) = await ProcessRunner.RunAsync("git", $"rebase origin/{parent}");
            if (exitCode == 0)
            {
                AnsiConsole.MarkupLine($"[green]Successfully rebased onto {parent}.[/]");
                if (!string.IsNullOrEmpty(output))
                    AnsiConsole.WriteLine(output);
            }
            else
            {
                AnsiConsole.MarkupLine("[red]Rebase failed.[/]");
                if (!string.IsNullOrEmpty(error))
                    AnsiConsole.WriteLine(error);
                AnsiConsole.MarkupLine("[dim]Run 'git rebase --abort' to undo, or resolve conflicts and 'git rebase --continue'.[/]");
            }
        });

        return command;
    }
}
