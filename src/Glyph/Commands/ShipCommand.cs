using System.CommandLine;
using Glyph.Services;
using Spectre.Console;

namespace Glyph.Commands;

public static class ShipCommand
{
    public static Command Create()
    {
        var messageArg = new Argument<string>("message") { Description = "Commit message" };

        var command = new Command("ship") { Description = "Stage all changes, commit, and push to origin" };
        command.Arguments.Add(messageArg);

        command.SetAction(async (ParseResult parseResult, CancellationToken ct) =>
        {
            var message = parseResult.GetValue(messageArg)!;
            using var git = new GitService();
            var current = git.CurrentBranchName;

            // Stage all changes
            var (addExit, _, addErr) = await ProcessRunner.RunAsync("git", "add -A");
            if (addExit != 0)
            {
                AnsiConsole.MarkupLine("[red]Failed to stage changes.[/]");
                if (!string.IsNullOrEmpty(addErr))
                    AnsiConsole.WriteLine(addErr);
                return;
            }

            // Commit
            var escapedMessage = message.Replace("\"", "\\\"");
            var (commitExit, commitOut, commitErr) = await ProcessRunner.RunAsync("git", $"commit -m \"{escapedMessage}\"");
            if (commitExit != 0)
            {
                AnsiConsole.MarkupLine("[red]Commit failed.[/]");
                if (!string.IsNullOrEmpty(commitErr))
                    AnsiConsole.WriteLine(commitErr);
                return;
            }

            AnsiConsole.MarkupLine("[green]Created commit.[/]");
            if (!string.IsNullOrEmpty(commitOut))
                AnsiConsole.WriteLine(commitOut);

            // Push
            AnsiConsole.MarkupLine($"Pushing [green]{current}[/] to origin...");
            var (pushExit, _, pushErr) = await ProcessRunner.RunAsync("git", $"push -u origin {current}");
            if (pushExit != 0)
            {
                AnsiConsole.MarkupLine($"[red]Push failed: {Markup.Escape(pushErr)}[/]");
                return;
            }

            AnsiConsole.MarkupLine("[green]Shipped![/]");
        });

        return command;
    }
}
