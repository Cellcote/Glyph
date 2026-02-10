using System.CommandLine;
using Glyph.Services;
using Spectre.Console;

namespace Glyph.Commands;

public static class CommitCommand
{
    public static Command Create()
    {
        var messageArg = new Argument<string?>("message") { Description = "Commit message", DefaultValueFactory = _ => null };
        var amendOption = new Option<bool>("--amend") { Description = "Amend the previous commit" };
        var allOption = new Option<bool>("-A") { Description = "Stage all changes before committing" };
        var aiOption = new Option<bool>("--ai") { Description = "Generate commit message using AI" };

        var command = new Command("commit") { Description = "Create a git commit" };
        command.Arguments.Add(messageArg);
        command.Options.Add(amendOption);
        command.Options.Add(allOption);
        command.Options.Add(aiOption);

        command.SetAction(async (ParseResult parseResult, CancellationToken ct) =>
        {
            var message = parseResult.GetValue(messageArg);
            var amend = parseResult.GetValue(amendOption);
            var addAll = parseResult.GetValue(allOption);
            var useAi = parseResult.GetValue(aiOption);

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

            // Resolve commit message if not provided
            if (string.IsNullOrEmpty(message))
            {
                if (useAi)
                {
                    message = await GenerateCommitMessageWithAi();
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

    internal static async Task<string?> GenerateCommitMessageWithAi()
    {
        var (diffExit, diff, diffErr) = await ProcessRunner.RunAsync("git", "diff --staged");
        if (diffExit != 0 || string.IsNullOrWhiteSpace(diff))
        {
            // Try unstaged diff as fallback (for ship command which stages after)
            (diffExit, diff, diffErr) = await ProcessRunner.RunAsync("git", "diff");
            if (diffExit != 0 || string.IsNullOrWhiteSpace(diff))
            {
                AnsiConsole.MarkupLine("[yellow]No changes found to generate a message from.[/]");
                return null;
            }
        }

        string? message = null;
        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync("Generating commit message...", async _ =>
            {
                try
                {
                    message = await AiService.GenerateCommitMessageAsync(diff);
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"[red]AI generation failed: {Markup.Escape(ex.Message)}[/]");
                }
            });

        if (message == null)
        {
            AnsiConsole.MarkupLine("[red]Failed to generate commit message.[/]");
            AnsiConsole.MarkupLine("[dim]Make sure GITHUB_TOKEN is set or 'gh' CLI is authenticated.[/]");
            return null;
        }

        AnsiConsole.MarkupLine($"[bold]Generated message:[/] {Markup.Escape(message)}");
        if (!AnsiConsole.Confirm("Use this message?", defaultValue: true))
        {
            message = AnsiConsole.Prompt(
                new TextPrompt<string>("Enter commit message:"));
            if (string.IsNullOrWhiteSpace(message))
            {
                AnsiConsole.MarkupLine("[red]Commit message cannot be empty.[/]");
                return null;
            }
        }

        return message;
    }
}
