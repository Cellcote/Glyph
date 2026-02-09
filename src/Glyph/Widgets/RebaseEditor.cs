using Glyph.Models;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Glyph.Widgets;

public class RebaseEditor
{
    private readonly List<CommitEntry> _commits;
    private int _cursor;
    private int _scrollOffset;
    private bool _editing;
    private string _editBuffer = "";

    public RebaseEditor(IReadOnlyList<CommitEntry> commits)
    {
        _commits = commits.ToList();
    }

    public RebaseEditorResult Run()
    {
        if (Console.IsInputRedirected)
        {
            AnsiConsole.MarkupLine("[red]Error:[/] Interactive mode requires a terminal.");
            return new RebaseEditorResult([], true);
        }

        var cursorWasVisible = true;
        try
        {
            try { Console.CursorVisible = false; }
            catch { cursorWasVisible = false; }

            RebaseEditorResult? result = null;

            AnsiConsole.Live(new Markup(""))
                .AutoClear(true)
                .Start(ctx =>
                {
                    while (true)
                    {
                        ctx.UpdateTarget(BuildView());
                        ctx.Refresh();

                        var key = Console.ReadKey(true);

                        if (_editing)
                        {
                            HandleEditingKey(key);
                            if (!_editing)
                                continue;
                            continue;
                        }

                        switch (key.Key)
                        {
                            case ConsoleKey.UpArrow:
                                if (_cursor > 0) _cursor--;
                                EnsureCursorVisible();
                                break;

                            case ConsoleKey.DownArrow:
                                if (_cursor < _commits.Count - 1) _cursor++;
                                EnsureCursorVisible();
                                break;

                            case ConsoleKey.P:
                                _commits[_cursor].Action = RebaseAction.Pick;
                                break;

                            case ConsoleKey.D:
                                _commits[_cursor].Action = RebaseAction.Drop;
                                break;

                            case ConsoleKey.S:
                                if (_cursor > 0)
                                    _commits[_cursor].Action = RebaseAction.Squash;
                                break;

                            case ConsoleKey.R:
                                _commits[_cursor].Action = RebaseAction.Reword;
                                break;

                            case ConsoleKey.RightArrow:
                                _commits[_cursor].Action = RebaseAction.Reword;
                                _editBuffer = _commits[_cursor].EditedMessage;
                                _editing = true;
                                break;

                            case ConsoleKey.Enter:
                                result = new RebaseEditorResult(_commits.AsReadOnly(), false);
                                return;

                            case ConsoleKey.Escape:
                                result = new RebaseEditorResult([], true);
                                return;
                        }
                    }
                });

            return result ?? new RebaseEditorResult([], true);
        }
        finally
        {
            try { Console.CursorVisible = cursorWasVisible; }
            catch { /* terminal may not support it */ }
        }
    }

    private void HandleEditingKey(ConsoleKeyInfo key)
    {
        switch (key.Key)
        {
            case ConsoleKey.Enter:
                _commits[_cursor].EditedMessage = _editBuffer;
                _editing = false;
                break;

            case ConsoleKey.Escape:
                _editing = false;
                break;

            case ConsoleKey.Backspace:
                if (_editBuffer.Length > 0)
                    _editBuffer = _editBuffer[..^1];
                break;

            default:
                if (key.KeyChar >= 32)
                    _editBuffer += key.KeyChar;
                break;
        }
    }

    private int ViewportHeight => Math.Max(Console.WindowHeight - 6, 5);

    private void EnsureCursorVisible()
    {
        if (_cursor < _scrollOffset)
            _scrollOffset = _cursor;
        else if (_cursor >= _scrollOffset + ViewportHeight)
            _scrollOffset = _cursor - ViewportHeight + 1;
    }

    private IRenderable BuildView()
    {
        var lines = new List<IRenderable>();

        lines.Add(new Markup(
            "[dim]↑↓[/] navigate  [dim]p[/] pick  [dim]d[/] drop  [dim]s[/] squash  " +
            "[dim]r[/] reword  [dim]→[/] edit msg  [dim]Enter[/] execute  [dim]Esc[/] cancel"));
        lines.Add(new Markup(""));

        var viewport = ViewportHeight;
        var end = Math.Min(_scrollOffset + viewport, _commits.Count);

        if (_scrollOffset > 0)
            lines.Add(new Markup($"[dim]  ↑ {_scrollOffset} more above[/]"));

        for (int i = _scrollOffset; i < end; i++)
        {
            var commit = _commits[i];
            var isCursor = i == _cursor;

            var cursor = isCursor ? ">" : " ";
            var actionTag = FormatAction(commit.Action);
            var hash = Markup.Escape(commit.ShortHash);
            var message = _editing && isCursor
                ? Markup.Escape(_editBuffer) + "[blink]|[/]"
                : Markup.Escape(commit.EditedMessage);

            var style = isCursor ? "bold" : "default";
            lines.Add(new Markup($"[{style}]{cursor} {actionTag}  [dim]{hash}[/] {message}[/]"));
        }

        var remaining = _commits.Count - end;
        if (remaining > 0)
            lines.Add(new Markup($"[dim]  ↓ {remaining} more below[/]"));

        lines.Add(new Markup(""));
        lines.Add(BuildSummary());

        return new Rows(lines);
    }

    private IRenderable BuildSummary()
    {
        var picks = _commits.Count(c => c.Action == RebaseAction.Pick);
        var drops = _commits.Count(c => c.Action == RebaseAction.Drop);
        var squashes = _commits.Count(c => c.Action == RebaseAction.Squash);
        var rewords = _commits.Count(c => c.Action == RebaseAction.Reword);

        var parts = new List<string>();
        if (picks > 0) parts.Add($"[green]{picks} pick[/]");
        if (drops > 0) parts.Add($"[red]{drops} drop[/]");
        if (squashes > 0) parts.Add($"[blue]{squashes} squash[/]");
        if (rewords > 0) parts.Add($"[cyan]{rewords} reword[/]");

        return new Markup($"[dim]{string.Join("  ", parts)}[/]");
    }

    private static string FormatAction(RebaseAction action) => action switch
    {
        RebaseAction.Pick => "[green]pick  [/]",
        RebaseAction.Drop => "[red]drop  [/]",
        RebaseAction.Squash => "[blue]squash[/]",
        RebaseAction.Reword => "[cyan]reword[/]",
        _ => "[dim]???   [/]"
    };
}

public record RebaseEditorResult(
    IReadOnlyList<CommitEntry> Commits,
    bool Cancelled);
