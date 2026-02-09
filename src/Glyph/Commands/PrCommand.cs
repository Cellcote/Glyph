using System.CommandLine;
using Glyph.Services;
using Spectre.Console;

namespace Glyph.Commands;

public static class PrCommand
{
    public static Command Create()
    {
        var titleOption = new Option<string?>("--title", "-t")
        {
            Description = "PR title (defaults to branch name)"
        };

        var command = new Command("pr") { Description = "Create a pull request into the parent branch" };
        command.Options.Add(titleOption);

        command.SetAction(async (ParseResult parseResult, CancellationToken ct) =>
        {
            var title = parseResult.GetValue(titleOption);
            using var git = new GitService();
            var current = git.CurrentBranchName;
            var parent = git.GetParentBranch(current);

            // Push current branch first
            AnsiConsole.MarkupLine($"Pushing [green]{current}[/] to origin...");
            var (pushExit, _, pushErr) = await ProcessRunner.RunAsync("git", $"push -u origin {current}");
            if (pushExit != 0)
            {
                AnsiConsole.MarkupLine($"[red]Push failed: {Markup.Escape(pushErr)}[/]");
                return;
            }

            // Create PR via gh CLI
            var prTitle = title ?? current.Replace("-", " ").Replace("/", ": ");
            AnsiConsole.MarkupLine($"Creating PR: [bold]{Markup.Escape(prTitle)}[/] -> [blue]{parent}[/]");

            var (exitCode, output, error) = await ProcessRunner.RunAsync(
                "gh", $"pr create --base {parent} --title \"{prTitle}\" --fill");

            if (exitCode == 0)
            {
                AnsiConsole.MarkupLine("[green]PR created![/]");
                AnsiConsole.WriteLine(output);
            }
            else
            {
                AnsiConsole.MarkupLine("[red]Failed to create PR.[/]");
                if (!string.IsNullOrEmpty(error))
                    AnsiConsole.WriteLine(error);
                AnsiConsole.MarkupLine("[dim]Make sure 'gh' CLI is installed and authenticated.[/]");
            }
        });

        return command;
    }
}
