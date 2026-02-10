using System.CommandLine;
using Glyph.Services;
using Spectre.Console;

namespace Glyph.Commands;

public static class UpdateCommand
{
    public static Command Create()
    {
        var command = new Command("update") { Description = "Update Glyph to the latest version" };

        command.SetAction(async (ParseResult parseResult, CancellationToken ct) =>
        {
            var current = UpdateChecker.GetCurrentVersion();

            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync("Checking for updates...", async ctx =>
                {
                    string? latest;
                    try
                    {
                        latest = await UpdateChecker.GetLatestVersionAsync();
                    }
                    catch
                    {
                        AnsiConsole.MarkupLine("[red]Failed to check for updates. Please check your internet connection.[/]");
                        return;
                    }

                    if (latest == null)
                    {
                        AnsiConsole.MarkupLine("[yellow]Could not determine the latest version.[/]");
                        return;
                    }

                    if (!UpdateChecker.IsNewerVersion(latest, current))
                    {
                        AnsiConsole.MarkupLine($"[green]Glyph is already up to date ({current}).[/]");
                        return;
                    }

                    AnsiConsole.MarkupLine($"Updating Glyph from {current} to {latest}...");
                    ctx.Status("Updating...");

                    var (exitCode, output, error) = await ProcessRunner.RunAsync(
                        "dotnet", "tool update --global Glyph");

                    if (exitCode == 0)
                    {
                        AnsiConsole.MarkupLine($"[green]Successfully updated Glyph to {latest}![/]");
                    }
                    else
                    {
                        AnsiConsole.MarkupLine("[red]Update failed.[/]");
                        if (!string.IsNullOrEmpty(error))
                            AnsiConsole.WriteLine(error);
                    }
                });
        });

        return command;
    }
}
