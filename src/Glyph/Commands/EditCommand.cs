using System.CommandLine;
using Glyph.Models;
using Glyph.Services;
using Glyph.Widgets;
using Spectre.Console;

namespace Glyph.Commands;

public static class EditCommand
{
    public static Command Create()
    {
        var command = new Command("edit") { Description = "Interactively rebase commits on the current branch" };

        command.SetAction(async (ParseResult parseResult, CancellationToken ct) =>
        {
            using var git = new GitService();
            var current = git.CurrentBranchName;
            var parent = git.GetParentBranch(current);

            var mergeBase = git.GetMergeBase(current, parent);
            if (mergeBase == null)
            {
                AnsiConsole.MarkupLine("[yellow]Could not determine merge base with parent branch.[/]");
                return;
            }

            var commits = git.GetBranchCommits(current, parent);
            if (commits.Count == 0)
            {
                AnsiConsole.MarkupLine("[dim]No commits on this branch to edit.[/]");
                return;
            }

            var editor = new RebaseEditor(commits);
            var result = editor.Run();

            if (result.Cancelled)
            {
                AnsiConsole.MarkupLine("[dim]Cancelled.[/]");
                return;
            }

            // Check if anything changed
            var hasChanges = result.Commits.Any(c =>
                c.Action != RebaseAction.Pick || c.EditedMessage != c.Message);

            if (!hasChanges)
            {
                AnsiConsole.MarkupLine("[dim]No changes to apply.[/]");
                return;
            }

            // Build rebase-todo content
            var todoLines = new List<string>();
            foreach (var commit in result.Commits)
            {
                var action = commit.Action switch
                {
                    RebaseAction.Pick => "pick",
                    RebaseAction.Drop => "drop",
                    RebaseAction.Squash => "squash",
                    RebaseAction.Reword => "reword",
                    _ => "pick"
                };
                todoLines.Add($"{action} {commit.ShortHash} {commit.Message}");
            }

            var tempDir = Path.Combine(Path.GetTempPath(), $"glyph-rebase-{Guid.NewGuid():N}");
            try
            {
                Directory.CreateDirectory(tempDir);

                var todoFile = Path.Combine(tempDir, "todo");
                await File.WriteAllTextAsync(todoFile, string.Join("\n", todoLines) + "\n", ct);

                // Build GIT_EDITOR script for reword commits with changed messages
                var rewordCommits = result.Commits
                    .Where(c => c.Action == RebaseAction.Reword && c.EditedMessage != c.Message)
                    .ToList();

                var envVars = new Dictionary<string, string>
                {
                    ["GIT_SEQUENCE_EDITOR"] = $"cp {EscapePath(todoFile)}"
                };

                if (rewordCommits.Count > 0)
                {
                    var counterFile = Path.Combine(tempDir, "counter");
                    await File.WriteAllTextAsync(counterFile, "0", ct);

                    // Write each new message to a numbered file
                    for (int i = 0; i < rewordCommits.Count; i++)
                    {
                        var msgFile = Path.Combine(tempDir, $"msg-{i}");
                        await File.WriteAllTextAsync(msgFile, rewordCommits[i].EditedMessage, ct);
                    }

                    // Create editor script that reads counter, copies the right message, increments
                    var editorScript = Path.Combine(tempDir, "editor.sh");
                    var scriptContent = $"""
                        #!/bin/sh
                        COUNTER=$(cat {EscapePath(counterFile)})
                        MSG_FILE="{Escape(tempDir)}/msg-$COUNTER"
                        if [ -f "$MSG_FILE" ]; then
                            cp "$MSG_FILE" "$1"
                        fi
                        echo $((COUNTER + 1)) > {EscapePath(counterFile)}
                        """;
                    await File.WriteAllTextAsync(editorScript, scriptContent, ct);

                    // Make executable
                    await ProcessRunner.RunAsync("chmod", $"+x {EscapePath(editorScript)}");

                    envVars["GIT_EDITOR"] = EscapePath(editorScript);
                }

                AnsiConsole.MarkupLine("[dim]Applying changes...[/]");

                var (exitCode, output, error) = await ProcessRunner.RunAsync(
                    "git", $"rebase -i {mergeBase}", environmentVariables: envVars);

                if (exitCode == 0)
                {
                    AnsiConsole.MarkupLine("[green]Rebase completed successfully.[/]");
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
            }
            finally
            {
                try { Directory.Delete(tempDir, true); }
                catch { /* cleanup best-effort */ }
            }
        });

        return command;
    }

    private static string EscapePath(string path) => $"\"{path.Replace("\"", "\\\"")}\"";
    private static string Escape(string value) => value.Replace("\"", "\\\"");
}
