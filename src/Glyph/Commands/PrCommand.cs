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
        var aiOption = new Option<bool>("--ai") { Description = "Generate PR title and description using AI" };

        var command = new Command("pr") { Description = "Create a pull request into the parent branch" };
        command.Options.Add(titleOption);
        command.Options.Add(aiOption);

        command.SetAction(async (ParseResult parseResult, CancellationToken ct) =>
        {
            var title = parseResult.GetValue(titleOption);
            var useAi = parseResult.GetValue(aiOption);
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

            string? body = null;

            if (useAi && string.IsNullOrEmpty(title))
            {
                // Generate both title and body via AI
                var generated = await GeneratePrWithAi(current, parent);
                if (generated != null)
                {
                    title = generated.Value.Title;
                    body = generated.Value.Body;
                }
            }

            // Fall back to defaults
            var prTitle = title ?? current.Replace("-", " ").Replace("/", ": ");

            AnsiConsole.MarkupLine($"Creating PR: [bold]{Markup.Escape(prTitle)}[/] -> [blue]{parent}[/]");

            var escapedTitle = prTitle.Replace("\"", "\\\"");
            string ghArgs;
            if (!string.IsNullOrEmpty(body))
            {
                var escapedBody = body.Replace("\"", "\\\"");
                ghArgs = $"pr create --base {parent} --title \"{escapedTitle}\" --body \"{escapedBody}\"";
            }
            else
            {
                ghArgs = $"pr create --base {parent} --title \"{escapedTitle}\" --fill";
            }

            var (exitCode, output, error) = await ProcessRunner.RunAsync("gh", ghArgs);

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

    private static async Task<(string Title, string Body)?> GeneratePrWithAi(string branchName, string parentBranch)
    {
        var (diffExit, diff, _) = await ProcessRunner.RunAsync("git", $"diff {parentBranch}...HEAD");
        if (diffExit != 0 || string.IsNullOrWhiteSpace(diff))
        {
            AnsiConsole.MarkupLine("[yellow]No changes found between branch and parent.[/]");
            return null;
        }

        (string Title, string Body)? result = null;
        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync("Generating PR description...", async _ =>
            {
                try
                {
                    result = await AiService.GeneratePrDescriptionAsync(diff, branchName, parentBranch);
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"[red]AI generation failed: {Markup.Escape(ex.Message)}[/]");
                }
            });

        if (result == null)
        {
            AnsiConsole.MarkupLine("[red]Failed to generate PR description.[/]");
            AnsiConsole.MarkupLine("[dim]Make sure GITHUB_TOKEN is set or 'gh' CLI is authenticated.[/]");
            return null;
        }

        AnsiConsole.MarkupLine($"[bold]Generated title:[/] {Markup.Escape(result.Value.Title)}");
        AnsiConsole.MarkupLine("[bold]Generated body:[/]");
        AnsiConsole.WriteLine(result.Value.Body);

        if (!AnsiConsole.Confirm("Use this PR description?", defaultValue: true))
            return null;

        return result;
    }
}
