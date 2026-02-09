using System.CommandLine;
using Glyph.Services;
using Spectre.Console;

namespace Glyph.Commands;

public static class CommitCommand
{
    public static Command Create()
    {
        var messageArg = new Argument<string>("message") { Description = "Commit message" };
        var amendOption = new Option<bool>("--amend") { Description = "Amend the previous commit" };
        var allOption = new Option<bool>("-A") { Description = "Stage all changes before committing" };

        var command = new Command("commit") { Description = "Create a git commit" };
        command.Arguments.Add(messageArg);
        command.Options.Add(amendOption);
        command.Options.Add(allOption);

        command.SetAction(async (ParseResult parseResult, CancellationToken ct) =>
        {
            var message = parseResult.GetValue(messageArg)!;
            var amend = parseResult.GetValue(amendOption);
            var addAll = parseResult.GetValue(allOption);

            if (addAll)
            {
                var (addExit, _, addErr) = await ProcessRunner.RunAsync("git", "add -A");
                if (addExit != 0)
                {
                    AnsiConsole.MarkupLine("[red]Failed to stage changes.[/]");
                    if (!string.IsNullOrEmpty(addErr))
                        AnsiConsole.WriteLine(addErr);
                    return;
                }
            }

            var escapedMessage = message.Replace("\"", "\\\"");
            var args = amend
                ? $"commit --amend -m \"{escapedMessage}\""
                : $"commit -m \"{escapedMessage}\"";

            var (exitCode, output, error) = await ProcessRunner.RunAsync("git", args);

            if (exitCode == 0)
            {
                AnsiConsole.MarkupLine(amend
                    ? "[green]Amended commit.[/]"
                    : "[green]Created commit.[/]");
                if (!string.IsNullOrEmpty(output))
                    AnsiConsole.WriteLine(output);
            }
            else
            {
                AnsiConsole.MarkupLine("[red]Commit failed.[/]");
                if (!string.IsNullOrEmpty(error))
                    AnsiConsole.WriteLine(error);
            }
        });

        return command;
    }
}
