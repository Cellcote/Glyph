using System.CommandLine;
using Glyph.Services;
using Spectre.Console;

namespace Glyph.Commands;

public static class ShipCommand
{
    public static Command Create()
    {
        var messageArg = new Argument<string?>("message") { Description = "Commit message", DefaultValueFactory = _ => null };
        var aiOption = new Option<bool>("--ai") { Description = "Generate commit message using AI" };

        var command = new Command("ship") { Description = "Stage all changes, commit, and push to origin" };
        command.Arguments.Add(messageArg);
        command.Options.Add(aiOption);

        command.SetAction(async (ParseResult parseResult, CancellationToken ct) =>
        {
            var message = parseResult.GetValue(messageArg);
            var useAi = parseResult.GetValue(aiOption);
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

            // Resolve commit message if not provided
            if (string.IsNullOrEmpty(message))
            {
                if (useAi)
                {
                    message = await CommitCommand.GenerateCommitMessageWithAi();
                    if (message == null)
                        return;
                }
                else
                {
                    message = AnsiConsole.Prompt(
                        new TextPrompt<string>("Enter commit message:"));
                    if (string.IsNullOrWhiteSpace(message))
                    {
                        AnsiConsole.MarkupLine("[red]Commit message cannot be empty.[/]");
                        return;
                    }
                }
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
