using System.CommandLine;
using Glyph.Models;
using Glyph.Services;
using Glyph.Widgets;
using Spectre.Console;

namespace Glyph.Commands;

public static class AddCommand
{
    public static Command Create()
    {
        var command = new Command("add") { Description = "Interactively select files to stage" };

        command.SetAction(async (ParseResult parseResult, CancellationToken ct) =>
        {
            using var git = new GitService();
            var files = git.GetUnstagedFiles();

            if (files.Count == 0)
            {
                AnsiConsole.MarkupLine("[dim]No unstaged changes found.[/]");
                return;
            }

            var selector = new FileSelector(files);
            var result = selector.Run();

            if (result.Cancelled)
            {
                AnsiConsole.MarkupLine("[dim]Cancelled.[/]");
                return;
            }

            if (result.Discard)
            {
                var path = result.SelectedPaths[0];
                AnsiConsole.MarkupLine($"Discarding changes to [yellow]{Markup.Escape(path)}[/]...");

                var (exitCode, _, error) = result.DiscardChangeKind == FileChangeKind.Added
                    ? await ProcessRunner.RunAsync("git", $"clean -f -- \"{path}\"")
                    : await ProcessRunner.RunAsync("git", $"checkout -- \"{path}\"");

                if (exitCode == 0)
                    AnsiConsole.MarkupLine($"[green]Discarded changes to {Markup.Escape(path)}.[/]");
                else
                {
                    AnsiConsole.MarkupLine("[red]Discard failed.[/]");
                    if (!string.IsNullOrEmpty(error))
                        AnsiConsole.WriteLine(error);
                }
                return;
            }

            if (result.SelectedPaths.Count == 0)
            {
                AnsiConsole.MarkupLine("[dim]No files selected.[/]");
                return;
            }

            var quoted = string.Join(" ", result.SelectedPaths.Select(p => $"\"{p}\""));
            var (addExit, _, addErr) = await ProcessRunner.RunAsync("git", $"add -- {quoted}");

            if (addExit == 0)
            {
                AnsiConsole.MarkupLine($"[green]Staged {result.SelectedPaths.Count} file(s).[/]");
                foreach (var path in result.SelectedPaths)
                    AnsiConsole.MarkupLine($"  [dim]{Markup.Escape(path)}[/]");
            }
            else
            {
                AnsiConsole.MarkupLine("[red]Staging failed.[/]");
                if (!string.IsNullOrEmpty(addErr))
                    AnsiConsole.WriteLine(addErr);
            }
        });

        return command;
    }
}
