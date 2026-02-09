using System.CommandLine;
using Glyph.Services;
using Spectre.Console;

namespace Glyph.Commands;

public static class SyncCommand
{
    public static Command Create()
    {
        var command = new Command("sync") { Description = "Fetch and rebase current branch onto its parent" };

        command.SetAction(async (ParseResult parseResult, CancellationToken ct) =>
        {
            using var git = new GitService();
            var current = git.CurrentBranchName;
            var parent = git.GetParentBranch(current);

            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync($"Syncing {current} with {parent}...", async ctx =>
                {
                    // Fetch all
                    ctx.Status("Fetching from origin...");
                    var (fetchExit, _, fetchErr) = await ProcessRunner.RunAsync("git", "fetch --all --prune");
                    if (fetchExit != 0)
                    {
                        AnsiConsole.MarkupLine($"[red]Fetch failed: {Markup.Escape(fetchErr)}[/]");
                        return;
                    }

                    // Rebase onto parent
                    ctx.Status($"Rebasing onto origin/{parent}...");
                    var (rebaseExit, rebaseOut, rebaseErr) = await ProcessRunner.RunAsync(
                        "git", $"rebase origin/{parent}");

                    if (rebaseExit == 0)
                    {
                        AnsiConsole.MarkupLine($"[green]Synced! {current} is now up to date with {parent}.[/]");
                    }
                    else
                    {
                        AnsiConsole.MarkupLine("[red]Rebase failed during sync.[/]");
                        if (!string.IsNullOrEmpty(rebaseErr))
                            AnsiConsole.WriteLine(rebaseErr);
                        AnsiConsole.MarkupLine("[dim]Resolve conflicts and run 'git rebase --continue', or 'git rebase --abort'.[/]");
                    }
                });
        });

        return command;
    }
}
